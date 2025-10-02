#region Copyright ©2016, Crane Nuclear, Inc. - All Rights Reserved
/* --------------------------------------------------------------------- *
                        Proprietary Information of
                            Crane Nuclear, Inc.

                    Copyright ©2016, Crane Nuclear Inc.
                           All Rights Reserved

            This document, and executable code generated from it
            are the property of Crane Nuclear, Inc. and is delivered
            on the express condition that it is not to be disclosed,
            reproduced, in whole or in part or used in development
            or manufacture without the written consent of Crane Nuclear, 
            Inc.  Crane Nuclear, Inc. grants no right to disclose or 
            use any information contained within this document.
* --------------------------------------------------------------------- */
#endregion

namespace Archiver
{
    using System.Security.Cryptography;

    /// <summary>
    /// Supports data obfuscation.
    /// </summary>
    internal static class Obfuscator
    {
        private static readonly byte[] Key = new byte []
        {
            0x6C, 0x95, 0x01, 0x8E, 0xEC, 0x8F, 0xF9, 0xDC,
            0x65, 0x69, 0xFE, 0x88, 0x55, 0x66, 0xF5, 0x7B,
            0xD9, 0x83, 0x07, 0x5E, 0xEB, 0x39, 0xD5, 0xF8,
            0xE5, 0x99, 0x07, 0xEC, 0xA5, 0xA0, 0xB7, 0xB6,
        };

        private static readonly byte[] Vector = new byte[]
        {
            0x31, 0x4B, 0x22, 0x8E, 0x65, 0x17, 0xCD, 0x5C,
            0x9B, 0x95, 0xC1, 0xE1, 0xA2, 0x42, 0x91, 0xD9,
        };

        /// <summary>
        /// Creates a standard encryptor.
        /// </summary>
        /// <returns></returns>
        internal static ICryptoTransform CreateEncryptor( )
        {
            var aes = Aes.Create( );
            aes.Padding = PaddingMode.PKCS7;
            return aes.CreateEncryptor( Key, Vector );
        }

        /// <summary>
        /// Creates a standard decryptor.
        /// </summary>
        /// <returns></returns>
        internal static ICryptoTransform CreateDecryptor( )
        {
            var aes = Aes.Create( );
            aes.Padding = PaddingMode.PKCS7;
            return aes.CreateDecryptor( Key, Vector );
        }
    }
}
