# Render core material override

The **Material** marker (`RenderCoreMaterialMarkerSettings`) replaces renderer materials with an explicit render core Standard shader variant. It is exported in the Binary format only; Wavefront ignores it.

## Authoring

The marker lists source materials of the renderer on its own object, or of all LOD renderers when the object has a `LODGroup`. Each entry selects a variant from `ModRenderCoreVariants` and stores its property values. **Export from material** (`RenderCoreMaterialBaker`) fills the values from HDRP Lit / Layered Lit:

- diffuse — Base Map multiplied by Base Color (the source map itself when the color is white);
- emission — Emissive Map tinted by Emissive Color normalized to 1, the HDR intensity goes to the emission parameters;
- normal — Normal Map, rebuilt as an RGB normal when Normal Scale differs from 1;
- packed map — channels assembled per variant layout from Mask Map (with smoothness, metallic and AO remapping), Base Map alpha (with Alpha Remapping, HDRP 17 formulas) and normal XY;
- alpha threshold — Alpha Clipping threshold.

Generated textures are saved as PNG assets in a `RenderCore` folder next to the material. Properties without an HDRP counterpart keep their defaults and are reported to the Console.

`ModRenderCoreVariants` mirrors rendercore `MaterialEditorSettings.asset` (name, id, queue, render type, keywords, properties) for map variants. Car, particle, SLOD and matte shadow variants are not offered.

## Export

During a Binary build `RenderCoreMaterialOverrides` substitutes each overridden material with a proxy whose stable id is the source id plus a hash of the override. The same source material without a marker, or with other values, therefore stays a separate material. The MTL entry of the proxy keeps the ordinary fallback and adds:

```
cx_rc <variant> <id>.rc.json
```

The `.rc.json` document (`ModRenderCoreMaterial`) stores the variant and the authored properties; textures are written next to it. The packer converts it to a binary document and its textures to `BinaryTexture`: `u_sTexture*` in sRGB, packed and normal maps linear. `BinaryMaterial.renderCoreVariant` and `renderCoreResource` reference it.

## Client

The client builds the material from its Standard shader template like `EditorUtils.SetupMaterialVariant`: variant keyword and additional keywords, render queue, `RenderType` tag and `materialVariant`. Only variants and properties of `ModRenderCoreVariants` are applied. An unknown variant, an unknown document version or a client without support uses the fallback material.
