# Streamed texture upload policy

Native `TextureExport` metadata describes the physical source bytes, not a
requested GPU conversion. Map Streamer copies those bytes into matching Unity
CPU storage without changing pixel format, component type, channel order or
color space.

`TextureExportInterop.CreateTexture` prefers the `TextureFormat` constructor
whenever Unity supports an exact legacy mapping of the source CPU layout.
Unity owns GPU expansion, swizzling, component conversion and decompression.
Unsupported GPU compression is also eligible when Unity provides a software
decompressor. No application-defined GPU conversion whitelist is imposed.

Formats without a usable legacy mapping, such as directly supported DXT3, use
the `GraphicsFormat` constructor instead.

`IsFormatSupportedForSampling` checks the original format when directly
sampleable. Otherwise it uses Unity's compatible sampling format, without
requiring matching channel counts, precision or component types. Sampling and,
when requested by the source filter, linear-filtering support are required.
Render-target support is not required. A directly sampleable but unfilterable
format does not gain filtering support merely because another compatible
format exists. A compatible sampling format does not guarantee that Unity has
the source decoder: texture construction remains authoritative and reports
unsupported input. For example, this Unity version cannot decode signed EAC
on Direct3D 11, although unsigned EAC and ETC2 software decompression work.

For example, Unity's RGB24 upload path retains three-byte CPU pixels and can
expand to RGBA8 on Direct3D 11 with opaque sampled alpha. The source remains
RGB24 throughout native copying, mip storage and pooling.

Only CPU layout must match exactly: a legacy constructor must round-trip to
the original graphics format before it can receive source bytes. For example,
BGR24 cannot be uploaded as RGB24 just because both contain three bytes per
pixel. Unity's GPU representation may differ freely, with its associated
performance and memory costs. No custom pixel conversion or GizmoSDK
conversion is performed, and the native source-format mapping is unchanged.

Texture creation verifies dimensions, physical format, mip count, readability
and total raw CPU byte count before native copying. Destination creation also
checks the physical format and dimensions against the source metadata before
exposing any CPU pointers. Pool keys and memory accounting continue to use
physical CPU storage, not Unity's GPU representation. If Unity cannot retain
the required CPU layout, the upload is rejected with a diagnostic rather than
copying incompatible bytes.

Native source-layout support is unchanged. In particular, RGB48 native input
remains unsupported. On Unity 6000.3.5f2 with Direct3D 11, RGB48 construction
preserves CPU bytes but produces incorrect GPU colors despite positive Unity
support queries. Broad compatibility is not a reason to enable new native
layouts based on capability queries alone.

Incomplete uncompressed source mip chains retain the existing base-level
fallback and Unity mip generation. Complete source chains are copied unchanged.
