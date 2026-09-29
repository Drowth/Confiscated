# Waiting-room AssetHub props

The low table and centre magazine in `SchoolLayout.unity` use the FBX meshes and
base-colour textures in the sibling folders. They were generated with AssetHub
from image-to-mesh workflows, then reduced to approximately 5,900 faces each
and converted to FBX with their axes baked for Unity.

- [Low table workflow](https://app.assethub.io/workflow/58908)
- [Magazine workflow](https://app.assethub.io/workflow/58909)

The source GLBs are retained under `D:/Confiscated/Docs/WaitingRoomAssets/`.
The Blender conversion script is in `Docs/WaitingRoomAssetConversion.py`.
The Editor menu item
`Confiscated/School Run/Apply Waiting Room AssetHub Props` fits these meshes to
the existing scene objects, preserving the two cream papers on the table.
