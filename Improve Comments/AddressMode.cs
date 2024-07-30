// Copyright © Plain Concepts S.L.U. All rights reserved. Use is subject to license terms.

namespace Evergine.Common.Graphics
{
    /// <summary>
    /// Specifies texture addressing mode.
    /// </summary>
    public enum AddressMode : byte
    {
        /// <summary>
        /// Point/nearest neighbor filtering, clamped texture coordinates.
        /// </summary>
        PointClamp = 0,

        /// <summary>
        /// Point/nearest neighbor filtering, wrapped texture coordinates.
        /// </summary>
        PointWrap,

        /// <summary>
        /// Bilinear filtering, clamped texture coordinates.
        /// </summary>
        LinearClamp,

        /// <summary>
        /// Bilinear filtering, wrapped texture coordinates.
        /// </summary>
        LinearWrap,

        /// <summary>
        /// Anisotropic filtering, clamped texture coordinates.
        /// </summary>
        AnisotropicClamp,

        /// <summary>
        /// Anisotropic filtering, wrapped texture coordinates.
        /// </summary>
        AnisotropicWrap,
    }
}
