# AssetReferenceBackend
Backend for the Relations inspector that shows where assets are referenced in the project.

##Notes
* the Asset reference backend loads and analyses all scenes of the project. This involves blocking calls, so Unity will be unresponsive for some time, depending on the number and size of your scenes.
* the graphs show all referenced Objects but not all reference connections. Any Object referenced by X can also be referenced by the Objects that lead from the root node to X. If the graphs shows a reference chain A->B->C, A might also reference C. Currently, the Unity API doesn't provide the exact data.
