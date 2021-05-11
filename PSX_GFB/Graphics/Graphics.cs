using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PSX.Devices {

    public class GPU {

        private uint GPUREAD;    

        private uint command;
        private int commandSize;
        private uint[] commandBuffer = new uint[16];
        private int pointer;

        private int scanLine = 0;

        private static readonly int[] resolutions = { 256, 320, 512, 640, 368 };
        private static readonly int[] dotClockDiv = { 10, 8, 5, 4, 7 };

        private VRAM vram = new VRAM(1024, 512); 
        private VRAM555 vram555 = new VRAM555(1024, 512); 

        public bool debug;

        private enum Mode {
            COMMAND,
            VRAM
        }

        private TextureData[] t = new TextureData[4];

        [StructLayout(LayoutKind.Explicit)]
        private struct Color {
            [FieldOffset(0)] public uint val;
            [FieldOffset(0)] public byte r;
            [FieldOffset(1)] public byte g;
            [FieldOffset(2)] public byte b;
            [FieldOffset(3)] public byte m;
        }
        private Color color0;
        private Color color1;
        private Color color2;

        private bool isTextureDisabledAllowed;

        
        private byte textureXBase;
        private byte textureYBase;
        private byte transparencyMode;
        private byte textureDepth;
        private bool isDithered;
        private bool isDrawingToDisplayAllowed;
        private int maskWhileDrawing;
        private bool checkMaskBeforeDraw;
        private bool isInterlaceField;
        private bool isReverseFlag;
        private bool isTextureDisabled;
        private byte horizontalResolution2;
        private byte horizontalResolution1;
        private bool isVerticalResolution480;
        private bool isPal;
        private bool is24BitDepth;
        private bool isVerticalInterlace;
        private bool isDisplayDisabled;
        private bool isInterruptRequested;
        private bool isDmaRequest;

        private bool isReadyToReceiveCommand;
        private bool isReadyToSendVRAMToCPU;
        private bool isReadyToReceiveDMABlock;

        private byte dmaDirection;
        private bool isOddLine;

        private bool isTexturedRectangleXFlipped;
        private bool isTexturedRectangleYFlipped;

        private uint textureWindowBits = 0xFFFF_FFFF;
        private int preMaskX;
        private int preMaskY;
        private int postMaskX;
        private int postMaskY;

        private ushort drawingAreaLeft;
        private ushort drawingAreaRight;
        private ushort drawingAreaTop;
        private ushort drawingAreaBottom;
        private short drawingXOffset;
        private short drawingYOffset;

        private ushort displayVRAMXStart;
        private ushort displayVRAMYStart;
        private ushort displayX1;
        private ushort displayX2;
        private ushort displayY1;
        private ushort displayY2;

        private int videoCycles;
        private int horizontalTiming = 3413;
        private int verticalTiming = 263;

        public GPU(HostWindow window) {
            this.window = window;
            mode = Mode.COMMAND;
            GP1_00_ResetGPU();
        }

         public uint loadGPUSTAT() {
            uint GPUSTAT = 0;

            GPUSTAT |= textureXBase;
            GPUSTAT |= (uint)textureYBase << 4;
            GPUSTAT |= (uint)transparencyMode << 5;
            GPUSTAT |= (uint)textureDepth << 7;
            GPUSTAT |= (uint)(isDithered ? 1 : 0) << 9;
            GPUSTAT |= (uint)(isDrawingToDisplayAllowed ? 1 : 0) << 10;
            GPUSTAT |= (uint)maskWhileDrawing << 11;
            GPUSTAT |= (uint)(checkMaskBeforeDraw ? 1 : 0) << 12;
            GPUSTAT |= (uint)(isInterlaceField ? 1 : 0) << 13;
            GPUSTAT |= (uint)(isReverseFlag ? 1 : 0) << 14;
            GPUSTAT |= (uint)(isTextureDisabled ? 1 : 0) << 15;
            GPUSTAT |= (uint)horizontalResolution2 << 16;
            GPUSTAT |= (uint)horizontalResolution1 << 17;
            GPUSTAT |= (uint)(isVerticalResolution480 ? 1 : 0);
            GPUSTAT |= (uint)(isPal ? 1 : 0) << 20;
            GPUSTAT |= (uint)(is24BitDepth ? 1 : 0) << 21;
            GPUSTAT |= (uint)(isVerticalInterlace ? 1 : 0) << 22;
            GPUSTAT |= (uint)(isDisplayDisabled ? 1 : 0) << 23;
            GPUSTAT |= (uint)(isInterruptRequested ? 1 : 0) << 24;
            GPUSTAT |= (uint)(isDmaRequest ? 1 : 0) << 25;

            GPUSTAT |= (uint);
            GPUSTAT |= (uint);
            GPUSTAT |= (uint);

            GPUSTAT |= (uint)dmaDirection << 29;
            GPUSTAT |= (uint)(isOddLine ? 1 : 0) << 31;

            //Console.WriteLine("[GPU] LOAD GPUSTAT: {0}", GPUSTAT.ToString("x8"));
            return GPUSTAT;
        }

        public uint loadGPUREAD() {
            //TODO check if correct and refact
            uint value;
            if (vram_coord.size > 0) {
                value = readFromVRAM();
            } else {
                value = GPUREAD;
            }
            //Console.WriteLine("[GPU] LOAD GPUREAD: {0}", value.ToString("x8"));
            return value;
        }

        public void write(uint addr, uint value) {
            uint register = addr & 0xF;
            if (register == 0) {
                writeGP0(value);
            } else if (register == 4) {
                writeGP1(value);
            } else {
                Console.WriteLine($"[GPU] Unhandled GPU write access to register {register} : {value}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void writeGP0(uint value) {
            //Console.WriteLine("Direct " + value.ToString("x8"));
            //Console.WriteLine(mode);
            if (mode == Mode.COMMAND) {
                DecodeGP0Command(value);
            } else {
                WriteToVRAM(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void processDma(Span<uint> buffer) {
            if (mode == Mode.COMMAND) {
                DecodeGP0Command(buffer);
            } else {
                for (int i = 0; i < buffer.Length; i++) {
                    WriteToVRAM(buffer[i]);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteToVRAM(uint value) { //todo rewrite this mess
            vram_coord.size--;

            ushort pixel1 = (ushort)(value >> 16);
            ushort pixel0 = (ushort)(value & 0xFFFF);

            pixel0 |= (ushort)(maskWhileDrawing << 15);
            pixel1 |= (ushort)(maskWhileDrawing << 15);

            drawVRAMPixel(pixel0);

            //Force exit if we arrived to the end pixel (fixes weird artifacts on textures on Metal Gear Solid)
            if (vram_coord.size == 0 && vram_coord.x == vram_coord.origin_x && vram_coord.y == vram_coord.origin_y + vram_coord.h) {
                mode = Mode.COMMAND;
                return;
            }

            drawVRAMPixel(pixel1);

            if (vram_coord.size == 0) {
                mode = Mode.COMMAND;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint readFromVRAM() {
            ushort pixel0 = vram.GetPixelBGR555(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF);
            ushort pixel1 = vram.GetPixelBGR555(vram_coord.x++ & 0x3FF, vram_coord.y & 0x1FF);
            if (vram_coord.x == vram_coord.origin_x + vram_coord.w) {
                vram_coord.x -= vram_coord.w;
                vram_coord.y++;
            }
            vram_coord.size -= 2;
            return (uint)(pixel1 << 16 | pixel0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void drawVRAMPixel(ushort val) {
            if (checkMaskBeforeDraw) {
                int bg = vram.GetPixelRGB888(vram_coord.x, vram_coord.y);

                if (bg >> 24 == 0) {
                    vram.SetPixel(vram_coord.x & 0x3FF, vram_coord.y & 0x1FF, color1555to8888(val));
                    vram1555.SetPixel(vram_coord.x & 0x3FF, vram_coord.y & 0x1FF, val);
                }
            } else {
                vram.SetPixel(vram_coord.x & 0x3FF, vram_coord.y & 0x1FF, color1555to8888(val));
                vram1555.SetPixel(vram_coord.x & 0x3FF, vram_coord.y & 0x1FF, val);
            }

            vram_coord.x++;

            if (vram_coord.x == vram_coord.origin_x + vram_coord.w) {
                vram_coord.x -= vram_coord.w;
                vram_coord.y++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecodeGP0Command(uint value) {
            if (pointer == 0) {
                command = value >> 24;
                commandSize = CommandSizeTable[(int)command];
                //Console.WriteLine("[GPU] Direct GP0 COMMAND: {0} size: {1}", value.ToString("x8"), commandSize);
            }

             private static ReadOnlySpan<byte> CommandSizeTable => new byte[] {
            //0  1   2   3   4   5   6   7   8   9   A   B   C   D   E   F
             1,  1,  3,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //0
             1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //1
             4,  4,  4,  4,  7,  7,  7,  7,  5,  5,  5,  5,  9,  9,  9,  9, //2
             6,  6,  6,  6,  9,  9,  9,  9,  8,  8,  8,  8, 12, 12, 12, 12, //3
             3,  3,  3,  3,  3,  3,  3,  3, 16, 16, 16, 16, 16, 16, 16, 16, //4
             4,  4,  4,  4,  4,  4,  4,  4, 16, 16, 16, 16, 16, 16, 16, 16, //5
             3,  3,  3,  1,  4,  4,  4,  4,  2,  1,  2,  1,  3,  3,  3,  3, //6
             2,  1,  2,  1,  3,  3,  3,  3,  2,  1,  2,  2,  3,  3,  3,  3, //7
             4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4, //8
             4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4, //9
             3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //A
             3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //B
             3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //C
             3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3, //D
             1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1, //E
             1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1  //F
        };
    }
}