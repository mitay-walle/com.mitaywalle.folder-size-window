# FolderSizeWindow
Unity3d EditorWindow, that show runtime size of folders in Project Window
![image](https://github.com/user-attachments/assets/354c43b2-9c0d-40bf-b112-ada39e09356b)
## Features
- Use [Profiler.GetRuntimeMemorySizeLong](https://docs.unity3d.com/ScriptReference/Profiling.Profiler.GetRuntimeMemorySizeLong.html) to calculate size
- Sorting Alphabetical / Size
- Asyncronous calculation
## Known Issues
- Show uncollapsed sub-subfolders from project left-side
- Prefabs size is incorrect (won't fix)
- Not calculate Scenes size (Profiler.GetRuntimeMemorySizeLong throw error, won't fix)

