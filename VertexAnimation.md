# Animation markers (dro2)

1. In MapUploader, add `GameMarkerData` and select `Animation`.
2. Assign the source `Animator` (the Animator on the marker object is used when the field is empty). Its controller must contain animation clips.
3. Choose the initial clip, bake frame rate, playback speed, initial phase and looping. All distinct clips in the controller are baked; each instance can start with a different clip.
4. Export as `dro2`. Rebuild both MapUploader and the client with the updated Creator module. Older clients cannot display animation resources.

The exporter creates `animations/<scene>.json`: stable mesh indices, material surfaces, per-instance transforms, and linear RGBAHalf position/normal/tangent atlases. Animation frames may occupy multiple rows. Clips share an atlas; frame interpolation stays within the selected clip. The existing OBJ path is not used for animated vertices. Matching baked geometry, animation and material data are deduplicated across instances, including objects with different world transforms or selected clips. Tangents are optional when loading older animation assets.

The client uses the `ModAnimation/ModVertexAnimation` resource material and `RenderCore/Mod Vertex Animation` shader. Mesh and material registrations are shared. Clip range and playback parameters are DOTS material properties, so instances with different phases/clips can be batched. No Animator, skinning or vertex upload runs on the CPU during playback. Each material/submesh still requires its own draw. Bounds contain every baked pose; forward, ghost, depth and shadow passes use the same deformation. Unloading unregisters the animation meshes/materials and destroys their textures.

## Supported scope

- Skinned mesh, blend-shape and rigid child-transform animation, evaluated as individual clips through Animation Playables. This is baked clip playback, not a runtime Animator state machine: transitions, parameters, events, procedural scripts and IK are not reproduced. Root-motion locomotion is disabled.
- Multiple renderers and triangle submeshes are combined in Animator space. Source vertex normals are required. Materials support diffuse texture/color, shared UV scale/offset, tangent-space normal maps with strength, metallic/smoothness, HDRP mask maps (metallic, AO, smoothness) with AO/smoothness remapping, alpha cutoff and double-sided rendering. Imported normal maps are decoded to linear RGB during export; animated tangents preserve their orientation as the mesh deforms. Layered blending, emission, detail maps and transparent blending are not exported by this material path.
- One mesh detail level per animation object; nested LODGroups and Animators are rejected. Animated objects can follow a Rigidbody on the Animator root or an ancestor. Add colliders for the rigid parts; the collider shapes do not deform with the animated vertices. See Rigidbody.md. Without Rigidbody, animated hierarchies remain decorative.
- 1–60 bake samples per second. Maximum atlas dimensions: 4096 × 8192 and the target GPU's texture limit; all three atlases together are limited to 128 MiB per asset. Reduce clip count, vertices or sampling rate when this is exceeded.
- GPU instancing shares identical prototypes; this is not MassRenderer's URP-specific multi-draw-indirect implementation. DR3 uses its existing Entities Graphics / RenderCore renderer.
