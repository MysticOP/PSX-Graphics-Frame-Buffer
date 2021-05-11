Using System;
Using System.Collections.Generic;
Using System.Drawing;
Using System.Linq;
Using System.Runtime.CompilerServices;
Using System.Windows.Forms;
Using System.Threading.Tasks;
Using System.Diagnostics;
Using System.Threading;
Using System.Runtime.InteropServices;

Namespace PSX {
    Public Interface HostWindow {
        void Render(int[] vram);
        void SetDisplayMode(int horizontalRes, int verticalRes, bool is24BitDepth);
		void SetDisplay(int  horizontalRes = 1280, int verticalRes = 720);
        void SetHorizontalRange(UShort displayX1, ushort displayX2);
        void SetVRAMStart(UShort displayVRAMXStart, ushort displayVRAMYStart);
        void SetVerticalRange(UShort displayY1, ushort displayY2);
        void Play(Byte[] samples);
    }
}


End Namespace

Namespace HostWindows
    Public Class Window :     Form, HostWindow {

        Private Const int PSX_MHZ = 33868800;
        Private Const int SYNC_CYCLES = 100;
        Private Const int MIPS_UNDERCLOCK = 3;

        Private Const int cyclesPerFrame = PSX_MHZ / 60;
        Private Const int syncLoops = (cyclesPerFrame / (SYNC_CYCLES * MIPS_UNDERCLOCK)) + 1;
        Private Const int cycles = syncLoops * SYNC_CYCLES;

        Private Size vramSize = New Size(1024, 512);
        Private Size _640x480 = New Size(640, 480);
        Private ReadOnly DoubleBufferedPanel screen = New DoubleBufferedPanel();

        Private GdiBitmap display = New GdiBitmap(1024, 512);

        Private PSX psx;
        Private int fps;
        Private bool isVramViewer;

        Private int horizontalRes;
        Private int verticalRes;

        Private int displayVRAMXStart;
        Private int displayVRAMYStart;

        Private bool is24BitDepth;

        Private int displayX1;
        Private int displayX2;
        Private int displayY1;
        Private int displayY2;


     Public Window() {
            text = "PSX"
            autosize = true
            autosizemode = autosizemode.growandshrink
            keyup += New keyeventhandley(vramviewertoggle)


            screen.size = _1270x720;
            screen.Margin = New Padding(0);
            screen.MouseDoubleClick += New MouseEventHandler(toggleDebug);


     Public Window() {
            text = "PSX"
            autosize = true
            autosizemode = autosizemode.growandshrink
            keyup += New keyeventhandley(vramviewertoggle)


            screen.size = _1920x1080
            screen.Margin = New Padding(0);
            screen.MouseDoubleClick += New MouseEventHandler(toggleDebug);


     Public Window() {
            text = "PSX"
            autosize = true
            autosizemode = autosizemode.growandshrink
            keyup += New keyeventhandley(vramviewertoggle)


            screen.size = _2560x1440
            screen.Margin = New Padding(0);
            screen.MouseDoubleClick += New MouseEventHandler(toggleDebug);




End Namespace