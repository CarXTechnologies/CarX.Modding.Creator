# Render core material override

The **Material** marker (`RenderCoreMaterialMarkerSettings`) replaces renderer materials with an explicit render core Standard shader variant. It is exported in the Binary format only; Wavefront ignores it.

## Authoring

The marker lists source materials of the renderer on its own object, or of all LOD renderers when the object has a `LODGroup`. Each entry selects a variant from `ModRenderCoreVariants.Offered` and stores its property values. A new entry gets a suggested variant (`RenderCoreMaterialBaker.SuggestVariant` over the mirror suggestions `ModRenderCoreVariants.Suggest`: render type from the HDRP surface and alpha clipping, normal map, emission). For alpha clipping it never suggests a variant whose clip channel also carries AO while another alpha-test variant exists (see [Alpha test](#alpha-test)). **Export from material** (`RenderCoreMaterialBaker`) fills the values from HDRP Lit / Layered Lit and records the variant revision it baked for (`RenderCoreMaterialOverride.revision`, see [Revision policy](#revision-policy)):

- diffuse — Base Map multiplied by Base Color (the source map itself when the color is white);
- emission — Emissive Map tinted by Emissive Color normalized to 1, the HDR intensity goes to the emission parameters. This covers every emission variant, including the road sign map of `MAT_ROAD_SIGN_EMISSION`;
- normal — Normal Map, rebuilt as an RGB normal when Normal Scale differs from 1; without a Normal Map a flat 4×4 normal is written, so the document never relies on the default of the client's template;
- packed map — channels assembled per variant layout from Mask Map (with smoothness, metallic and AO remapping), Base Map alpha (with Alpha Remapping, HDRP 17 formulas) and normal XY.

Generated textures are saved as PNG assets in a `RenderCore` folder next to the material. Values without an HDRP counterpart keep their defaults and are reported to the Console as "Set manually":

- properties with source `None` (for example `u_vColor`, `u_vThirdColor`, layer multipliers);
- the color mask channel of `MAT_COLORMASK_*` packed maps: it is baked as 0 (no tint) and has to be painted by the author;
- the HDRP Alpha Clipping threshold when it differs from the HDRP default 0.5 (see [Alpha test](#alpha-test));
- an AO × alpha packed channel when texels that are opaque in HDRP have AO below 1: occlusion lowers their alpha-test coverage (see [Alpha test](#alpha-test)).

## Variant table: the render core mirror

`ModRenderCoreVariants` is generated from rendercore, not written by hand. rendercore owns the id, queue (base + offset), `RenderType`, additional keywords and the property list with types, labels, tooltips and vector labels. The keyword is the enum name. The SDK owns which variants are offered and what their textures mean.

| File | Kind | Content |
| --- | --- | --- |
| `Editor/RenderCoreMirror/RenderCoreLock.txt` | generated, committed | Normalized, sorted text snapshot of the rendercore inputs: `MaterialVariant` enum, `MaterialEditorSettings.asset`, `EditorUtils.SetupMaterialVariant`, `Standard.shader` (properties, `[Gamma]`, per-pass pragmas), `StandardMaterialDefinitions.hlsl`, `Definitions/*MaterialDefs.hlsl` and `Functions/*MaterialFunc.hlsl` (per-variant defines, vertex streams and a decode hash). Provenance lines (package version, commit) are not compared. |
| `Editor/RenderCoreMirror/Generation/RenderCoreMirrorAnnotations.cs` | hand-edited | The only hand input: scope (excluded `MAT_CAR_*`, `MAT_PARTICLE*`, `MAT_SLOD`, `MAT_MATTESHADOW`, `MAT_SKIDMARK`, `MAT_UNSUPPORTED`), offered variants with layout key, packed channels, reviewed decode hash and vertex colour usage, slot overrides with reasons, the reviewed `SetupMaterialVariant` hash, frozen baked contracts per revision and the variant suggestions. |
| `Runtime/RenderCore/ModRenderCoreVariantTable.g.cs` | generated, do not edit | `CreateVariants()`, `CreateSuggestions()` and `LockContentHash`. Its header lists rendercore map variants that exist but are not annotated, therefore not offered. |
| `Runtime/RenderCore/ModRenderCoreVariants.cs` | hand-written | API (`All`, `Offered`, `TryGet`, `TryFind`, `Suggest`), `SupportedStreams` and the frozen color-space rule baked into published mods: `u_sTexture*` is sRGB, every other texture is linear (`ModRenderCoreVariantProperty.IsNormalMap` is the matching frozen `u_sNormalMap*` rule). |
| `Tools~/RenderCoreMirror` | tool | net8 command line build of the same extractor and generator; reads rendercore from a directory or with `git show <ref>:<path>`. |

The generator derives role, layer and default of each property from slot rules (`u_sTexture` diffuse, `u_sTexture1` emission in base-layout emission variants, layer 1 in `MAT_LAYERS4` and no HDRP source in `MAT_INTERIOR` (a window atlas in room space), `u_sPackedMap*` packed map, `u_sNormalMap*` normal, `u_fParams` emission parameters in emission variants), the vertex streams from `INPUT_NEED_*`, and cross-checks the annotated channels against the `MATERIALFEATURE_*` defines. A variant that needs a stream mod meshes do not carry is emitted with an `unavailableReason`: it stays in `All`, but not in `Offered`, and `TryGet` does not return it.

### Updating

Menus (Unity, `ModSystem/Render Core`):

- **Regenerate Table** — `generate(committed lock, annotations)`; writes `ModRenderCoreVariantTable.g.cs` only when it changed. Works in MapUploader. Use it after editing the annotations.
- **Update Lock From Project Render Core** — extracts from the `com.carxtech.rendercore` package of the project, rewrites `RenderCoreLock.txt` and regenerates. Available only where the package is installed (the game project); greyed out in MapUploader.
- **Check** — writes nothing, logs the findings by tier.

Regenerate Table and Update write nothing while a generator gate (`RCM0xx`, the current lock against the annotations) reports Breaking or Error: they log each code and show a dialog. Update also prints how the new lock differs from the committed one (`RCM1xx`) for review; these differences do not block it (see [Finding tiers](#finding-tiers)).

Command line, without Unity (`dotnet run --project Tools~/RenderCoreMirror -- <command>`):

- `extract --rendercore <dir> | --git <repo> --ref <sha> [--override <file-name>=<ref>]... --out <lock>`
- `check` (same sources) `--sdk <dir>` — exit code 0 clean or Info only, 2 Stale, 3 Breaking, 4 Error;
- `update` (same sources) `--sdk <dir>` — writes the lock and the table, like the menu;
- `generate --sdk <dir> [--check]`;
- `legacy-diff --old-dll <CarX.Modding.Creator.dll>` — field-by-field diff of an old hand table against the generated one;
- `replay --git <repo> --cases replay-cases.json` — runs the history cases.

Details are in `Tools~/RenderCoreMirror/README.md`.

In the game project the mirror is checked, never written: the `RenderCoreMirrorUpToDateTests` test (category `Validation`, run by BuildFlow) fails on Stale, Breaking or Error, and a pre-build guard throws on Breaking or Error in non-development builds (an error log in development builds; `CARX_MODDING_SKIP_RENDERCORE_MIRROR_CHECK=1` skips it). The fix is always: run **Update Lock From Project Render Core**, commit `CarX.Modding.Creator`, bump the submodule. In MapUploader the Validation test is ignored; the SDK tests there prove that the committed table equals `generate(committed lock, annotations)`.

### Finding tiers

| Tier | Examples | Effect |
| --- | --- | --- |
| Info | label, tooltip, vector labels, shader default literal, non-layout defines (`SINGLE_RENDER_TARGET`, `IGNORE_WET_PROCESS`, …), property order, a new non-annotated variant, moved files, the Func file name | never fails a check; picked up at the next update |
| Stale | id, queue, render type, additional keywords, an added or removed property, a property type, vertex streams, keyword declarations in passes, `[Gamma]` of a non-texture property | the check fails until the lock is updated; **Update** heals it. Where such a change is unsafe — a property of an unknown slot, a type the slot does not accept, a stream mod meshes do not carry, a keyword declared as `shader_feature*` or missing where the variant needs it — the generator gates report it as Breaking as well |
| Breaking | an offered variant missing or renamed, asset label ≠ enum name, `RenderType` enum members, layout key (defs and `MATERIALFEATURE_*` / `PACKED_MESH*` defines, alpha-test depth and shadow pass configuration), decode hash (active Func lines and Definitions libraries), `[Gamma]` of a texture against the frozen rule, `SetupMaterialVariant` body; from the generator gates also the unsafe Stale cases above and a baked contract changed without a revision bump | exactly what the generator gates refuse, so **Update** refuses it too and a lock written by Update is never Breaking for the check; needs a reviewed annotation change, usually a revision bump |
| Error | a rendercore input that cannot be located or parsed | nothing is guessed; the parser has to be extended |

To offer a new rendercore variant: update the lock, add a variant annotation (the generator prints its layout key, decode hash and derived channels), add its contract revision 1, regenerate, bump the submodules. Clients without it fall back to the MTL material.

### Revision policy

Each offered variant has a `revision` (≥ 1) of its baked contract: packed channel layout, texture color space and vertex colour usage, i.e. everything that is baked into exported textures.

- **Mechanical changes heal by regeneration.** Documents store only the variant name; a client re-reads id, queue, render type and keywords from its own table. Added properties need no bump either: old documents use the template value.
- **Baked-contract changes need a revision bump.** Edit the annotation, increment `revision` and append a new contract entry; contract entries are append-only and pinned by a test. There is no runtime remap: documents with an older revision fall back, per material, to their MTL material until the author re-runs **Export from material** on the marker and rebuilds the map.
- **The export writes the revision the values were baked for, not the table's.** Export from material stores it in the marker entry (`RenderCoreMaterialOverride.revision`; 0, as in entries made before the field existed, means 1). The `.rc.json` `variantRevision` and `cx_rc_rev` carry that stored revision, so a rebuild after an SDK update never stamps textures in the old layout with the new revision; the export warns "re-run Export from material" when the stored revision is older than the table's. Packed maps assigned by hand count as baked for the stored revision too.
- Revision 1 is the table after the fixes of this release (`u_vThirdColor` has no HDRP source, `MAT_ROUGHNESS_ALPHABLEND` packed B is alpha, `MAT_COLORMASK_*` packed B is the color mask, `MAT_ROAD_SIGN_EMISSION` `u_sTexture1` is emission, `MAT_INTERIOR` unavailable). It is frozen before the first public mod release; internal test maps exported earlier must be re-exported.

## Variant notes

### Alpha test

Alpha-test variants clip on packed map B against a fixed, dithered threshold defined by the render core shader. The HDRP Alpha Clipping threshold is not transferred; Export from material reports it when it differs from the HDRP default 0.5. `u_vThirdColor.a` only sets the alpha written to the color target, not the threshold.

In `MAT_ROUGHNESS_ALPHATEST` packed B is AO × alpha: the same value is the occlusion and the clip input, so texels that are opaque in HDRP but occluded by the Mask Map can be dithered away or cut. The suggestion therefore picks `MAT_ROUGHNESS_NORMAL_ALPHATEST` (B is alpha only) for alpha-clipped materials, also without a normal map: Export from material then writes a flat normal, and the Mask Map AO is not carried over. An author who picks an AO × alpha layout anyway gets a manual-list note when such texels exist.

### MAT_LAYERS4

Layer blend weights come from the vertex colour: rgb is the layer mask, a tints the albedo (`vertexColors = LayerBlend`). A mesh without vertex colours is exported white and shows only the fourth layer. The client keeps vertex colours for such meshes; a mesh that mixes `MAT_LAYERS4` and emission submeshes keeps its colours as well, so its emission is multiplied by them (warned). Build optimization merges a renderer that uses a layer-blend variant only with other layer-blend renderers: the vertex colour usage (`RenderCoreMaterialOverrides.GetVertexColorUsage`) is part of its render compatibility key, so a separate emissive object never ends up in its mesh. Only a source mesh that itself mixes both is affected. Emission counts here only for opaque MTL materials, as on the client: transparency and alpha test take precedence over emission there.

### Emission

Emission variants multiply emission by the vertex colour (rgb) and use its alpha as the flicker phase (`vertexColors = EmissionMultiplier`). Road sign (`MAT_ROAD_SIGN_EMISSION`) and window (`MAT_WINDOW`) emission are scaled by game globals, not by the mod material, so they are visible only under game conditions: road signs light up only at night with lights on, windows follow the global window intensity and are masked by the global window emission map. Expect them to look unlit in daylight.

### MAT_INTERIOR

Interior mapping reads a second UV set (`TEXCOORD1`); mod meshes carry only UV0. The variant is unavailable until the mesh format gains UV1: it is not offered, the MapUploader scene validator reports existing markers as errors, the export writes only the fallback material and the client never applies it.

## Export

During a Binary build `RenderCoreMaterialOverrides` substitutes each material with an available variant by a proxy whose stable id is the source id plus a hash of the canonical variant name, the authored values sorted by property name (a property reorder in rendercore does not change ids) and, from revision 2, the baked revision. The same source material without a marker, or with other values, therefore stays a separate material. The MTL entry of the proxy keeps the ordinary fallback and adds:

```
cx_rc <variant> <id>.rc.json
cx_rc_rev <revision>
```

`cx_rc` carries the canonical variant name; `cx_rc_rev` carries the baked revision of the marker entry and is written only for revisions above 1, so revision 1 output is unchanged. An unknown or unavailable variant gets no proxy, no document and a warning (once per marker entry and build): only the fallback material is exported.

The `.rc.json` document (`ModRenderCoreMaterial`, version 1) stores the variant, `variantRevision` (the baked revision; missing or 0 means 1) and the authored properties; textures are written next to it. The packer converts it to a binary document and its textures to `BinaryTexture` by the frozen rule: `u_sTexture*` in sRGB, packed and normal maps linear. `BinaryMaterial.renderCoreVariant`, `renderCoreResource` and `renderCoreRevision` (0 means 1) reference it.

## Client

The client builds the material from its Standard shader template like `EditorUtils.SetupMaterialVariant`: variant keyword and additional keywords, render queue, `RenderType` tag and `materialVariant`. It applies a document only when the variant is available in its table, the revision matches, and the keywords exist in the shipped shader. Otherwise that one material uses its fallback MTL material; so does an unknown document version, a client without support, or a texture that fails to load (for example a color-space mismatch) — the rest of the map loads normally.
