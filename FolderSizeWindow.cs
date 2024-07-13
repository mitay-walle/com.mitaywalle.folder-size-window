using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace Plugins.Editor
{
	public enum Sorting
	{
		Alphabetical,
		Size,
	}

	public sealed class FolderSizeWindow : EditorWindow, IHasCustomMenu
	{
		private static MethodInfo _tryGetActiveFolderPath = typeof(ProjectWindowUtil).GetMethod("TryGetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
		private static HashSet<Type> _excluded = new()
		{
			typeof(SceneAsset),
			//typeof(DefaultAsset),
		};
		private List<Drawer> _drawers = new();
		private string _lastPath;
		private Sorting _sorting;
		private Vector2 _scroll;
		private int _maxloadCount;
		private int _loadCount;

		[MenuItem("Window/Analysis/Folder Size")]
		private static void Open()
		{
			var w = GetWindow<FolderSizeWindow>();
			if (!w) CreateWindow<FolderSizeWindow>();
		}

		private void OnEnable()
		{
			titleContent = new GUIContent("Folder Size");
			EditorApplication.projectWindowItemOnGUI -= OnDrawItem;
			EditorApplication.projectWindowItemOnGUI += OnDrawItem;
		}

		private void OnDisable() => EditorApplication.projectWindowItemOnGUI -= OnDrawItem;
		public void Update() => Repaint();

		private void OnGUI()
		{
			TryGetActiveFolderPath(out string path);
			if (_lastPath != path)
			{
				_lastPath = path;
				_drawers.Clear();
			}

			EditorGUILayout.Space();
			if (EditorGUILayout.LinkButton("Profiler.GetRuntimeMemorySizeLong used to calculate size"))
			{
				Application.OpenURL("https://docs.unity3d.com/ScriptReference/Profiling.Profiler.GetRuntimeMemorySizeLong.html");
			}
			EditorGUILayout.Space();
			_sorting = (Sorting)EditorGUILayout.EnumPopup(_sorting);
			EditorGUILayout.Space();
			switch (_sorting)
			{
				case Sorting.Alphabetical:
					{
						_drawers.Sort((d1, d2) => d2.Name.CompareTo(d1.Name));
						break;
					}
				case Sorting.Size:
					{
						_drawers.Sort((d1, d2) =>
						{
							int result = d1.Size.CompareTo(d2.Size);
							if (result != 0) return result;
							return d1.Guid.CompareTo(d2.Guid);
						});
						break;
					}
				default: throw new ArgumentOutOfRangeException();
			}

			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			for (int i = _drawers.Count - 1; i >= 0; i--)
			{
				Drawer drawer = _drawers[i];
				drawer.OnGUI();
			}
			EditorGUILayout.EndScrollView();
		}

		private void OnDrawItem(string guid, Rect selectionRect)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (!path.Contains(_lastPath)) return;
			if (path == _lastPath) return;

			var drawer = _drawers.Find(d => d.Guid == guid);
			if (drawer != null) return;
			drawer = new Drawer(this, guid, path);
			_drawers.Add(drawer);
		}

		private static bool TryGetActiveFolderPath(out string path)
		{
			object[] args = { null };
			bool found = (bool)_tryGetActiveFolderPath.Invoke(null, args);
			path = (string)args[0];

			return found;
		}

		void IHasCustomMenu.AddItemsToMenu(GenericMenu menu) => menu.AddItem(new GUIContent("Edit Script"), true, OpenScript);
		private void OpenScript() => AssetDatabase.OpenAsset(MonoScript.FromScriptableObject(this));

		public sealed class Drawer : IDisposable
		{
			public string Guid { get; }
			public string Name { get; }
			public Object Asset { get; }
			private EditorCoroutine _coroutine;
			public long Size { get; private set; }
			private string _path;
			private FolderSizeWindow _window;

			public Drawer(FolderSizeWindow window, string guid, string path)
			{
				_window = window;
				Guid = guid;
				_path = path;
				_coroutine = EditorCoroutineUtility.StartCoroutine(Calculate(), window);
				Name = Path.GetFileName(_path);
				Asset = AssetDatabase.LoadAssetAtPath<Object>(_path);
			}

			public void OnGUI()
			{
				GUILayout.BeginHorizontal();
				//EditorGUILayout.ObjectField(Asset, typeof(Object), false);
				//GUILayout.FlexibleSpace();
				EditorGUILayout.SelectableLabel(Name, GUILayout.Height(15));
				EditorGUILayout.SelectableLabel(SizeText.ByteLongToString(Size), GUILayout.Width(80), GUILayout.Height(15));
				GUILayout.EndHorizontal();
			}

			private IEnumerator Calculate()
			{
				if (Asset is DefaultAsset folder)
				{
					string[] guids = AssetDatabase.FindAssets("", new[] { _path });
					foreach (string guid in guids)
					{
						string path = AssetDatabase.GUIDToAssetPath(guid);
						if (string.IsNullOrEmpty(path)) continue;
						AddAllAssetsRecursive(path);
						yield return null;
					}
				}
				else
				{
					if (Asset && !_excluded.Contains(Asset.GetType()))
						AddAllAssetsRecursive(_path);
				}
			}

			private void AddAllAssetsRecursive(string path)
			{
				var asset = AssetDatabase.LoadAssetAtPath<Object>(path);

				if (!asset) return;

				if (_excluded.Contains(asset.GetType()))
				{
					return;
				}
				Object[] assets = null;

				try
				{
					assets = AssetDatabase.LoadAllAssetsAtPath(path);
				}
				catch
				{
					Debug.LogError($"unsupported asset type '{asset.GetType().Name}'", asset);
				}

				foreach (Object subAsset in assets)
				{
					if (subAsset && !_excluded.Contains(subAsset.GetType()))
					{
						Size += Profiler.GetRuntimeMemorySizeLong(subAsset);
					}
				}
				_window.Repaint();
			}

			public void Dispose()
			{
				if (_coroutine != null) EditorCoroutineUtility.StopCoroutine(_coroutine);
				_coroutine = null;
			}
		}
	}

	public static class SizeText
	{
		private static string[] names = { "byte", "kb", "Mb", "Gb" };
		private static double[] sizes = { 1.0d, 1024.0d, 1048576.0d, 1073741824.0d };

		public static string ByteLongToString(long sizeInBytes)
		{
			if (sizeInBytes <= 0) return "0 byte";

			int i = Mathf.RoundToInt(Mathf.Log(sizeInBytes, 1024));
			double size = sizeInBytes / sizes[i];
			return $"{size:0.00} {names[i]}";
		}
	}
}
