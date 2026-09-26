using System.IO;
using Unity.Collections;
using UnityEngine;

public sealed class FileReader
{
    public async Awaitable<NativeArray<byte>> ReadAsync(string path)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path);

        var nativeBytes = new NativeArray<byte>(
            bytes.Length,
            Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);

        NativeArray<byte>.Copy(bytes, nativeBytes);

        return nativeBytes;
    }
}