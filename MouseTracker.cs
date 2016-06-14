namespace TrackMouse
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Input;
    class MouseTracker : DependencyObject
    {
        private class MouseMoveEventArgs : MouseEventArgs
        {
            public Point Position { get; private set; }

            public MouseMoveEventArgs(Point position) : base(Mouse.PrimaryDevice, 0)
            {
                this.Position = position;
            }
        }


        private static Point _globalLastPosition;
        private static Point _localLastPosition;

        /// <summary>
        /// Retrieves the cursor's position, in screen coordinates.
        /// </summary>
        /// <see>See MSDN documentation for further information.</see>
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);

            return new Point(lpPoint.x, lpPoint.y);
        }

        #region Mouse Hook

        public delegate int HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        //Declare the hook handle as an int.
        static int hHookLL = 0;

        static int hHook = 0;

        //Declare MouseHookProcedure as a HookProc type.
        HookProc MouseHookProcedureLL;

        HookProc MouseHookProcedure;

        //values from Winuser.h in Microsoft SDK.
        /// <summary>
        /// Windows NT/2000/XP: Installs a hook procedure that monitors low-level mouse input events.
        /// </summary>
        private const int WH_MOUSE_LL = 14;

        /// <summary>
        /// Installs a hook procedure that monitors mouse messages. For more information, see the MouseProc hook procedure. 
        /// </summary>
        private const int WH_MOUSE = 7;


        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseLLHookStruct
        {
            public POINT Point;
            public int MouseData;
            public int Flags;
            public int Time;
            public int ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseHookStruct
        {
            public POINT Point;
            public IntPtr Hwnd;
            public uint HitTestCode;
            public int ExtraInfo;
        }


        //This is the Import for the SetWindowsHookEx function.
        //Use this function to install a thread-specific hook.
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);

        //This is the Import for the UnhookWindowsHookEx function.
        //Call this function to uninstall the hook.
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int UnhookWindowsHookEx(int idHook);

        //This is the Import for the CallNextHookEx function.
        //Use this function to pass the hook information to the next hook procedure in chain.
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private delegate void MouseMoveEventHandler(object sender, MouseMoveEventArgs e);

        /// <summary>
        /// Occurs when the mouse pointer is moved. 
        /// </summary>
        private static event MouseMoveEventHandler GlobalMouseMoveHandler;

        private static event MouseMoveEventHandler LocalMouseMoveHandler;

        #endregion Mouse Hook

        public Point LocalPosition
        {
            get { return (Point)GetValue(LocalPositionProperty); }
            set { SetValue(LocalPositionProperty, value); }
        }
        
        public static readonly DependencyProperty LocalPositionProperty =
            DependencyProperty.Register("LocalPosition", typeof(Point), typeof(MouseTracker), new PropertyMetadata(null));


        public Point GlobalPosition
        {
            get { return (Point)GetValue(GlobalPositionProperty); }
            set { SetValue(GlobalPositionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for GlobalPosition.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GlobalPositionProperty =
            DependencyProperty.Register("GlobalPosition", typeof(Point), typeof(MouseTracker), new PropertyMetadata(null));


        public bool Hooked
        {
            get { return (bool)GetValue(HookedProperty); }
            set { SetValue(HookedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Hooked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HookedProperty =
            DependencyProperty.Register("Hooked", typeof(bool), typeof(MouseTracker), new PropertyMetadata(false));


        public ICommand ToggleMouseHookCommand { get; private set; }

        public MouseTracker()
        {
            ToggleMouseHookCommand = new RelayCommand(x => ToggleMouseHook(), x => true);
        }
        

        private void ToggleMouseHook()
        {            
            if (!this.Hooked)
            {
                HookLL();
                Hook();
            }
            else
            {
                UnHookLL();
                UnHook();
            }

            this.Hooked = !this.Hooked;
        }

        private void HookLL()
        {
            // Create an instance of HookProc.
            MouseHookProcedureLL = new HookProc(MouseHookProcLL);
            hHookLL = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProcedureLL, (IntPtr)0, 0);

            //If SetWindowsHookEx fails.
            if (hHookLL == 0)
            {
                // Returns the error code returned by the last unmanaged function called 
                // using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                int errorCode = Marshal.GetLastWin32Error();

                // Initializes and throws a new instance of 
                // the Win32Exception class with the specified error. 
                throw new Win32Exception(errorCode);
            }

            GlobalMouseMoveHandler += OnGlobalMouseMove;
        }

        private void UnHookLL()
        {
            if (hHookLL != 0)
            {
                int result = UnhookWindowsHookEx(hHookLL);

                hHookLL = 0;

                MouseHookProcedureLL = null;

                //if failed and exception must be thrown
                if (result == 0)
                {
                    // Returns the error code returned by the last unmanaged function 
                    // called using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                    int errorCode = Marshal.GetLastWin32Error();

                    // Initializes and throws a new instance of the 
                    // Win32Exception class with the specified error. 
                    throw new Win32Exception(errorCode);
                }

                GlobalMouseMoveHandler -= OnGlobalMouseMove;
            }
        }
        
        private void Hook()
        {
            MouseHookProcedure = new HookProc(MouseHookProc);

            uint threadId = GetCurrentThreadId(); // AppDomain.GetCurrentThreadId(); //  <- Deprecated

            hHook = SetWindowsHookEx(WH_MOUSE, MouseHookProcedure, (IntPtr) 0, (int)threadId);

            //If SetWindowsHookEx fails.
            if (hHook == 0)
            {
                // Returns the error code returned by the last unmanaged function called 
                // using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                int errorCode = Marshal.GetLastWin32Error();

                // Initializes and throws a new instance of 
                // the Win32Exception class with the specified error. 
                throw new Win32Exception(errorCode);
            }

            LocalMouseMoveHandler += OnLocalMouseMove;
        }

        private void UnHook()
        {
            if (hHook != 0)
            {
                int result = UnhookWindowsHookEx(hHook);

                hHook = 0;

                MouseHookProcedure = null;

                //if failed and exception must be thrown
                if (result == 0)
                {
                    // Returns the error code returned by the last unmanaged function 
                    // called using platform invoke that has the DllImportAttribute.SetLastError flag set. 
                    int errorCode = Marshal.GetLastWin32Error();

                    // Initializes and throws a new instance of the 
                    // Win32Exception class with the specified error. 
                    throw new Win32Exception(errorCode);
                }

                LocalMouseMoveHandler -= OnLocalMouseMove;
            }
        }

        private void OnGlobalMouseMove(object sender, MouseMoveEventArgs e)
        {
            this.GlobalPosition = new Point(e.Position.X, e.Position.Y);
        }

        private void OnLocalMouseMove(object sender, MouseMoveEventArgs e)
        {
            this.LocalPosition = new Point(e.Position.X, e.Position.Y);

            Point cursorPosition = GetCursorPosition();

            if (cursorPosition.X != this.LocalPosition.X || cursorPosition.Y != this.LocalPosition.Y)
            {
                Debug.Print("Position mismatch (Local: x = " +
                    this.LocalPosition.X + " y = " + this.LocalPosition.Y +
                    " | Cursor: x = " + cursorPosition.X + " y = " + cursorPosition.Y + " )");
            }
        }


        private static int MouseHookProcLL(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Marshall the data from the callback.
                MouseLLHookStruct hookStruct = (MouseLLHookStruct)Marshal.PtrToStructure(lParam, typeof(MouseLLHookStruct));

                if (GlobalMouseMoveHandler != null)
                {
                    var e = new MouseMoveEventArgs(new Point(hookStruct.Point.x, hookStruct.Point.y));
                    
                    if (_globalLastPosition.X != e.Position.X || _globalLastPosition.Y != e.Position.Y)
                    {
                        GlobalMouseMoveHandler.Invoke(null, e);

                        _globalLastPosition = new Point(e.Position.X, e.Position.Y);
                    }
                }

            }

            return CallNextHookEx(hHookLL, nCode, wParam, lParam);
        }

        private static int MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Marshall the data from the callback.
                MouseHookStruct hookStruct = (MouseHookStruct)Marshal.PtrToStructure(lParam, typeof(MouseHookStruct));

                if (LocalMouseMoveHandler != null)
                {
                    var e = new MouseMoveEventArgs(new Point(hookStruct.Point.x, hookStruct.Point.y));

                    if (_localLastPosition.X != e.Position.X || _localLastPosition.Y != e.Position.Y)
                    {
                        LocalMouseMoveHandler.Invoke(null, e);

                        _localLastPosition = new Point(e.Position.X, e.Position.Y);
                    }
                }

            }

            return CallNextHookEx(hHookLL, nCode, wParam, lParam);
        }
    }


}
