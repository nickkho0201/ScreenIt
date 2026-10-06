using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Windows.Graphics.Capture ABI + D3D11 readback. No SDK projection/package or
// desktop/PrintWindow fallback. All COM/GPU work lives on one MTA worker.
internal static unsafe class WindowCapture
{
    internal static int LiveSessions;
    private static readonly Guid ItemId=new("79c3f95b-31f7-4ec2-a464-632ef5d30760");
    private static readonly Guid ClosableId=new("30d5a829-7fa4-4026-83bb-d75bae4ea99e");
    internal static Task<BitmapSource> Capture(WindowTarget target,CancellationToken token,uint? excludedProcess=null)
        => Task.Run(()=>Acquire(target,token,excludedProcess),token);
    private static BitmapSource Acquire(WindowTarget target,CancellationToken token,uint? excludedProcess)
    {
        token.ThrowIfCancellationRequested();
        if(WindowTargets.Read(target.Hwnd,excludedProcess) is not { } current || current.ProcessId!=target.ProcessId) throw new IOException("Window target disappeared.");
        int initialized=RoInitialize(1);HR(initialized);
        nint device=0,context=0,dxgi=0,winDevice=0,itemFactory=0,item=0,poolFactory=0,pool=0,session=0,options=0;
        long eventToken=0;bool subscribed=false;using var arrival=new Arrival();
        Interlocked.Increment(ref LiveSessions);
        try
        {
            HR(D3D11CreateDevice(0,1,0,0x20,0,0,7,out device,out _,out context));
            dxgi=Query(device,new("54ec77fa-1377-44e6-8c32-88fd5f44c84c"));
            HR(CreateDirect3D11DeviceFromDXGIDevice(dxgi,out winDevice));
            itemFactory=Factory("Windows.Graphics.Capture.GraphicsCaptureItem",new("3628e81b-3cac-4c60-b7f4-23ce0e0c3356"));
            var iid=ItemId;HR(((delegate* unmanaged[Stdcall]<nint,nint,Guid*,nint*,int>)Slot(itemFactory,3))(itemFactory,target.Hwnd,&iid,&item));
            Size size;HR(((delegate* unmanaged[Stdcall]<nint,Size*,int>)Slot(item,7))(item,&size));ValidateSize(size);
            poolFactory=Factory("Windows.Graphics.Capture.Direct3D11CaptureFramePool",new("589b103f-6bbc-5df5-a991-02e28b3b66d5"));
            HR(((delegate* unmanaged[Stdcall]<nint,nint,int,int,Size,nint*,int>)Slot(poolFactory,6))(poolFactory,winDevice,87,2,size,&pool));
            HR(((delegate* unmanaged[Stdcall]<nint,nint,long*,int>)Slot(pool,8))(pool,arrival.Pointer,&eventToken));subscribed=true;
            HR(((delegate* unmanaged[Stdcall]<nint,nint,nint*,int>)Slot(pool,10))(pool,item,&session));
            options=Query(session,new("2c39ae40-7d2e-5044-804e-8b6799d4cf9e"));
            HR(((delegate* unmanaged[Stdcall]<nint,byte,int>)Slot(options,7))(options,0)); // No cursor in output.
            HR(((delegate* unmanaged[Stdcall]<nint,int>)Slot(session,6))(session));
            long deadline=Environment.TickCount64+4000;
            var waits=new[]{arrival.Signal,token.WaitHandle};
            while(true)
            {
                token.ThrowIfCancellationRequested();int remaining=(int)Math.Max(0,deadline-Environment.TickCount64);
                int wake=WaitHandle.WaitAny(waits,remaining);
                if(wake==WaitHandle.WaitTimeout) throw new TimeoutException("No capturable window frame.");
                token.ThrowIfCancellationRequested();nint frame=0;
                HR(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Slot(pool,7))(pool,&frame));
                if(frame==0) continue; // Event-driven, no polling loop.
                try
                {
                    Size content;HR(((delegate* unmanaged[Stdcall]<nint,Size*,int>)Slot(frame,8))(frame,&content));
                    if(content.Width!=size.Width || content.Height!=size.Height) throw new IOException("Window resized during capture.");
                    var image=Readback(device,context,frame,content);token.ThrowIfCancellationRequested();
                    if(WindowTargets.Read(target.Hwnd,excludedProcess) is not { } latest || latest.ProcessId!=target.ProcessId) throw new IOException("Window target disappeared.");
                    return image;
                }
                finally { Close(frame);Release(ref frame); }
            }
        }
        finally
        {
            try
            {
                if(subscribed) HR(((delegate* unmanaged[Stdcall]<nint,long,int>)Slot(pool,9))(pool,eventToken));
            }
            finally
            {
                Close(session);Close(pool);Release(ref options);Release(ref session);Release(ref pool);Release(ref poolFactory);Release(ref item);Release(ref itemFactory);Release(ref winDevice);Release(ref dxgi);Release(ref context);Release(ref device);
                Interlocked.Decrement(ref LiveSessions);RoUninitialize();
            }
        }
    }
    private static BitmapSource Readback(nint device,nint context,nint frame,Size size)
    {
        nint surface=0,access=0,texture=0,staging=0;
        try
        {
            HR(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Slot(frame,6))(frame,&surface));
            access=Query(surface,new("a9b3d012-3df2-4ee3-b8d1-8695f457d3c1"));var iid=new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
            HR(((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)Slot(access,3))(access,&iid,&texture));
            TextureDesc desc;((delegate* unmanaged[Stdcall]<nint,TextureDesc*,void>)Slot(texture,10))(texture,&desc);
            if(desc.Width<size.Width || desc.Height<size.Height || desc.Format!=87) throw new IOException("Unexpected window surface.");
            desc.Usage=3;desc.BindFlags=0;desc.CpuAccessFlags=0x20000;desc.MiscFlags=0;
            HR(((delegate* unmanaged[Stdcall]<nint,TextureDesc*,nint,nint*,int>)Slot(device,5))(device,&desc,0,&staging));
            ((delegate* unmanaged[Stdcall]<nint,nint,nint,void>)Slot(context,47))(context,staging,texture);
            Mapped mapped;HR(((delegate* unmanaged[Stdcall]<nint,nint,uint,int,uint,Mapped*,int>)Slot(context,14))(context,staging,0,1,0,&mapped));
            try
            {
                int stride=checked(size.Width*4);var pixels=new byte[checked(stride*size.Height)];
                for(int y=0;y<size.Height;y++) Marshal.Copy(mapped.Data+(nint)(y*mapped.RowPitch),pixels,y*stride,stride);
                var bitmap=BitmapSource.Create(size.Width,size.Height,96,96,PixelFormats.Bgra32,null,pixels,stride);bitmap.Freeze();return bitmap;
            }
            finally { ((delegate* unmanaged[Stdcall]<nint,nint,uint,void>)Slot(context,15))(context,staging,0); }
        }
        finally { Release(ref staging);Release(ref texture);Release(ref access);Release(ref surface); }
    }
    private static void ValidateSize(Size s) { if(s.Width<2 || s.Height<2 || (long)s.Width*s.Height>40_000_000) throw new IOException("Unsupported window size."); }
    private static nint Slot(nint p,int index)=>((nint*)*(nint*)p)[index];
    private static void HR(int hr)=>Marshal.ThrowExceptionForHR(hr);
    private static nint Query(nint p,Guid iid) { nint result;HR(((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)Slot(p,0))(p,&iid,&result));return result; }
    private static nint Factory(string name,Guid iid)
    {
        HR(WindowsCreateString(name,name.Length,out nint str));try { HR(RoGetActivationFactory(str,ref iid,out nint p));return p; }finally { WindowsDeleteString(str); }
    }
    private static void Release(ref nint p) { if(p==0) return;Marshal.Release(p);p=0; }
    private static void Close(nint p)
    {
        if(p==0) return;Guid iid=ClosableId;nint close=0;
        if(((delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)Slot(p,0))(p,&iid,&close)>=0)
            try { int hr=((delegate* unmanaged[Stdcall]<nint,int>)Slot(close,6))(close);if(hr<0) System.Diagnostics.Trace.TraceError("ScreenIt WGC Close failed: {0:X}",hr); }finally { Release(ref close); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Width,Height; }
    [StructLayout(LayoutKind.Sequential)] private struct TextureDesc { public uint Width,Height,Mips,ArraySize,Format,SampleCount,SampleQuality,Usage,BindFlags,CpuAccessFlags,MiscFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct Mapped { public nint Data;public uint RowPitch,DepthPitch; }
    [DllImport("combase.dll")] private static extern int RoInitialize(uint type);
    [DllImport("combase.dll")] private static extern void RoUninitialize();
    [DllImport("combase.dll",CharSet=CharSet.Unicode)] private static extern int WindowsCreateString(string value,int length,out nint str);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint str);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint name,ref Guid iid,out nint factory);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgi,out nint device);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter,uint driver,nint software,uint flags,nint levels,uint count,uint sdk,out nint device,out uint level,out nint context);

    // Native agile event sink. WinRT's COM references pin Signal until the last
    // callback/reference is gone; no UI/capture work or blocking in Invoke.
    private sealed class Arrival : IDisposable
    {
        private static readonly nint Vtable=MakeVtable();
        internal readonly AutoResetEvent Signal=new(false);
        internal nint Pointer { get; private set; }
        internal Arrival()
        {
            Pointer=Marshal.AllocHGlobal(24);*(nint*)Pointer=Vtable;*(int*)(Pointer+8)=1;
            *(nint*)(Pointer+16)=GCHandle.ToIntPtr(GCHandle.Alloc(this));
        }
        private static nint MakeVtable()
        {
            var p=(nint*)Marshal.AllocHGlobal(32);p[0]=(nint)(delegate* unmanaged[Stdcall]<nint,Guid*,nint*,int>)&QI;p[1]=(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Add;p[2]=(nint)(delegate* unmanaged[Stdcall]<nint,uint>)&Drop;p[3]=(nint)(delegate* unmanaged[Stdcall]<nint,nint,nint,int>)&Invoke;return (nint)p;
        }
        [UnmanagedCallersOnly(CallConvs=new[]{typeof(CallConvStdcall)})] private static int QI(nint p,Guid* iid,nint* result)
        {
            *result=0;if(*iid!=new Guid("00000000-0000-0000-c000-000000000046") && *iid!=new Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90") && *iid!=new Guid("51a947f7-79cf-5a3e-a3a5-1289cfa6dfe8")) return unchecked((int)0x80004002);
            Interlocked.Increment(ref *(int*)(p+8));*result=p;return 0;
        }
        [UnmanagedCallersOnly(CallConvs=new[]{typeof(CallConvStdcall)})] private static uint Add(nint p)=>(uint)Interlocked.Increment(ref *(int*)(p+8));
        [UnmanagedCallersOnly(CallConvs=new[]{typeof(CallConvStdcall)})] private static uint Drop(nint p)
        {
            int count=Interlocked.Decrement(ref *(int*)(p+8));if(count==0) { var handle=GCHandle.FromIntPtr(*(nint*)(p+16));((Arrival)handle.Target!).Signal.Dispose();handle.Free();Marshal.FreeHGlobal(p); }return (uint)count;
        }
        [UnmanagedCallersOnly(CallConvs=new[]{typeof(CallConvStdcall)})] private static int Invoke(nint p,nint sender,nint args)
        {
            try { ((Arrival)GCHandle.FromIntPtr(*(nint*)(p+16)).Target!).Signal.Set();return 0; }catch { return unchecked((int)0x80004005); }
        }
        public void Dispose() { if(Pointer==0) return;var p=Pointer;Pointer=0;((delegate* unmanaged[Stdcall]<nint,uint>)Slot(p,2))(p); }
    }
}
