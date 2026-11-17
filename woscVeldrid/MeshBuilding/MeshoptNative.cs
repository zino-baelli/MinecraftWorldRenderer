// using System.Runtime.InteropServices;
//
// namespace woscVeldrid;
//
// public static class MeshoptNative
// {
//     // [DllImport("meshopt.dll", CallingConvention = CallingConvention.Cdecl)]
//     [DllImport(@"C:\Users\zino\RiderProjects\woscVeldrid\woscVeldrid\dll\meshopt.dll", CallingConvention = CallingConvention.Cdecl)]
//
//     public static extern UIntPtr meshopt_simplify_wrapper(
//         uint[] destination,
//         uint[] indices,
//         UIntPtr index_count,
//         float[] vertex_positions,
//         UIntPtr vertex_count,
//         UIntPtr vertex_stride,
//         UIntPtr target_index_count,
//         float target_error,
//         uint options,
//         out float result_error
//     );
// }
using System;
using System.Runtime.InteropServices;

public static class MeshoptNative
{
    // const string DLL = @"C:\Users\zino\RiderProjects\woscVeldrid\woscVeldrid\dll\meshopt.dll";
    const string DLL = "meshopt";


    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern UIntPtr meshopt_generateVertexRemap_wrapper(
        uint[] destination,
        uint[] indices,
        UIntPtr index_count,
        IntPtr vertices,
        UIntPtr vertex_count,
        UIntPtr vertex_size);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern void meshopt_remapIndexBuffer_wrapper(
        uint[] destination,
        uint[] indices,
        UIntPtr index_count,
        uint[] remap);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern void meshopt_remapVertexBuffer_wrapper(
        IntPtr destination,
        IntPtr vertices,
        UIntPtr vertex_count,
        UIntPtr vertex_size,
        uint[] remap);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern UIntPtr meshopt_simplify_wrapper(
        uint[] destination,
        uint[] indices,
        UIntPtr index_count,
        float[] vertex_positions,
        UIntPtr vertex_count,
        UIntPtr vertex_stride,
        UIntPtr target_index_count,
        float target_error,
        uint options,
        out float result_error);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern UIntPtr meshopt_simplifySloppy_wrapper(
        uint[] destination,
        uint[] indices,
        UIntPtr index_count,
        float[] vertex_positions,
        UIntPtr vertex_count,
        UIntPtr vertex_stride,
        UIntPtr target_index_count,
        float target_error);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern float meshopt_simplifyScale_wrapper(
        float[] vertex_positions,
        UIntPtr vertex_count,
        UIntPtr vertex_stride);
}