# Layered PBR material resources

The SDK exports 2–4 UV0 layers to a versioned `.pbr.json` beside the OBJ/MTL files. The MTL `cx_pbr` entry references that material. Each layer preserves base color, texture scale and offset, tangent-space normals and strength, metallic, smoothness and ambient-occlusion remapping. Mask maps retain the HDRP channel layout. Texture paths use asset identity rather than object names.

Layer masks use HDRP ordering (base in alpha, upper layers in RGB). Vertex colors are exported as `vc r g b a` records immediately after each OBJ vertex, preserved through face remapping, and combined with the mask using the material's Add/Multiply mode. Blending gives upper layers priority, matching HDRP's ordinary mask blend.

Height/density blending, main-layer influence and mapping other than UV0 are rejected during export. These modes require additional runtime support; they are not silently reduced to the first layer.

Existing MTL files continue to load. Their diffuse `-s` X/Y scale and `-o` X/Y offset are applied once to the mesh UVs, including negative values. Files without options use identity UVs. Exported layered materials retain the original UVs so every layer can apply its own transform in the shader.

Maps built before layered material export need to be rebuilt to include the missing layers and vertex colors. Older clients ignore `cx_pbr` and use the ordinary MTL first-layer fallback.
