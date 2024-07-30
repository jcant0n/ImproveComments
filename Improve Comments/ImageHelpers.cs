// Copyright © Plain Concepts S.L.U. All rights reserved. Use is subject to license terms.

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Evergine.Framework.Assets.Extensions
{
    /// <summary>
    /// This static class represent the image helpers.
    /// </summary>
    public static class ImageHelpers
    {
        /// <summary>
        /// Read int16 from binaryReader.
        /// </summary>
        /// <param name="binaryReader">binary reader.</param>
        /// <returns>int16 data.</returns>
        public static short ReadLittleEndianInt16(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(short)];

            for (int i = 0; i < sizeof(short); i += 1)
            {
                bytes[sizeof(short) - 1 - i] = binaryReader.ReadByte();
            }

            return BitConverter.ToInt16(bytes, 0);
        }

        /// <summary>
        /// Read UInt16 from binary reader.
        /// </summary>
        /// <param name="binaryReader">binary reader.</param>
        /// <returns>uint16 data.</returns>
        public static ushort ReadLittleEndianUInt16(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(ushort)];

            for (int i = 0; i < sizeof(ushort); i += 1)
            {
                bytes[sizeof(ushort) - 1 - i] = binaryReader.ReadByte();
            }

            return BitConverter.ToUInt16(bytes, 0);
        }

        /// <summary>
        /// Read int32 from binary reader.
        /// </summary>
        /// <param name="binaryReader">binary reader.</param>
        /// <returns>int32 data.</returns>
        public static int ReadLittleEndianInt32(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(int)];
            for (int i = 0; i < sizeof(int); i += 1)
            {
                bytes[sizeof(int) - 1 - i] = binaryReader.ReadByte();
            }

            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// Starts with.
        /// </summary>
        /// <param name="thisBytes">source byte array.</param>
        /// <param name="thatBytes">pattern byte array.</param>
        /// <returns>if thisBytes start with thatBytes.</returns>
        public static bool StartsWith(byte[] thisBytes, byte[] thatBytes)
        {
            for (int i = 0; i < thatBytes.Length; i += 1)
            {
                if (thisBytes[i] != thatBytes[i])
                {
                    return false;
                }
            }

            return true;
        }

#if IOS
        /// <summary>
        /// Gets the rgba bytes from an image stream.
        /// </summary>
        /// <param name="imageStream">The source image stream.</param>
        /// <returns>An array containing the premultiplied RGBA bytes of the raw image</returns>
        public static byte[] GetRGBABytes(Stream imageStream)
        {
            byte[] outputData = null;

            using (var uiImage = UIImage.LoadFromData(NSData.FromStream(imageStream)))
            {
                var cgImage = uiImage.CGImage;

                var imageWidth = (int)cgImage.Width;
                var imageHeight = (int)cgImage.Height;

                outputData = new byte[imageWidth * imageHeight * 4];

                using (var colorSpace = CGColorSpace.CreateDeviceRGB())
                using (var bitmapContext = new CGBitmapContext(outputData, imageWidth, imageHeight, 8, imageWidth * 4, colorSpace, CGBitmapFlags.PremultipliedLast))
                {
                    bitmapContext.DrawImage(new RectangleF(0, 0, imageWidth, imageHeight), cgImage);
                }
            }

            return outputData;
        }
#endif

        /// <summary>
        /// Convert BGRA to RGBA.
        /// </summary>
        /// <param name="bytes">The brga bytes.</param>
        /// <returns>The rgba bytes.</returns>
        public static byte[] FromBGRA32ToRGBA32(ref byte[] bytes)
        {
            // BGRA to RGBA
            for (int k = 0; k < bytes.Length; k += 4)
            {
                var sourceBlue = bytes[k];
                bytes[k] = bytes[k + 2];
                bytes[k + 2] = sourceBlue;
            }

            return bytes;
        }

        /// <summary>
        /// Convert RGB to RGBA.
        /// </summary>
        /// <param name="bytes">The RGB bytes.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <returns>The RGBA bytes.</returns>
        public static byte[] FromRGB24ToRGBA32(byte[] bytes, int width, int height)
        {
            // RGB to RGBA
            byte[] rgba = new byte[width * height * 4];

            int index = 0;
            for (int i = 0; i < bytes.Length; i += 3)
            {
                rgba[index++] = bytes[i];
                rgba[index++] = bytes[i + 1];
                rgba[index++] = bytes[i + 2];
                rgba[index++] = 1;
            }

            return rgba;
        }

        /// <summary>
        /// Convert BGR to RGBA.
        /// </summary>
        /// <param name="bytes">The BGR bytes.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <returns>The RGBA bytes.</returns>
        public static byte[] FromBGR24ToRGBA32(byte[] bytes, int width, int height)
        {
            // BGR to RGBA
            byte[] rgba = new byte[width * height * 4];

            int index = 0;
            for (int i = 0; i < bytes.Length; i += 3)
            {
                rgba[index++] = bytes[i + 2];
                rgba[index++] = bytes[i + 1];
                rgba[index++] = bytes[i];
                rgba[index++] = 1;
            }

            return rgba;
        }

        /// <summary>
        /// Read struct from a binary reader.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="reader">The binary reader.</param>
        /// <returns>The readed struct value.</returns>
        public static unsafe T ReadStruct<T>(BinaryReader reader)
        {
            int size = Unsafe.SizeOf<T>();
            byte* bytes = stackalloc byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = reader.ReadByte();
            }

            return Unsafe.Read<T>(bytes);
        }
    }
}
