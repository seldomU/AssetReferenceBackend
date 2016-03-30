using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RelationsInspector.Extensions;

namespace RelationsInspector.Backend.AssetDependency
{
	using UnityEditor;
	using ObjNodeGraph = Dictionary<ObjectNode, HashSet<ObjectNode>>;

	[AcceptTargets( typeof( Object ) )]
	class AssetReferenceBackend : MinimalBackend<ObjectNode, string>
	{
		static Color sceneNodeColor = new Color( 0.29f, 0.53f, 0.28f );
		static bool showLegend;

		// linking objects to the ones they reference
		ObjNodeGraph referenceGraph;

		// root directory for scene asset search
		string sceneDirPath = Application.dataPath;
		string[] sceneFilePaths;
		string searchString;

		public override void Awake( GetAPI getAPI )
		{
			referenceGraph = new ObjNodeGraph();
			base.Awake( getAPI );
		}

		public override IEnumerable<ObjectNode> Init( object target )
		{
			var targetObj = target as Object;

			var targets = new[] { targetObj }.ToHashSet();

			//var sceneGraphs = sceneFilePaths.Select( path => ObjectDependencyUtil.GetReferenceGraph( path, targets ) );

			var sceneGraphs = new[] { ObjectDependencyUtil.GetActiveSceneReferenceGraph( targets ) };

			// when all sceneGraphs are empty, add a dummy node to represent the target
			if ( sceneGraphs.All( x => !x.Keys.Any() ) )
			{
				string targetNodeName = targetObj.name + "\n(unreferenced)";
				var dummyNode = new ObjectNode( targetNodeName, targetNodeName, new[] { targetObj }, false );
				var dummyGraph = new ObjNodeGraph();
				dummyGraph[ dummyNode ] = new HashSet<ObjectNode>();
				sceneGraphs = new[] { dummyGraph };
			}

			foreach ( var g in sceneGraphs )
				ObjectDependencyUtil.AddGraph( referenceGraph, g );

			System.GC.Collect();
			return ObjectGraphUtil.GetRoots( referenceGraph );
		}

		public override IEnumerable<Relation<ObjectNode, string>> GetRelations( ObjectNode entity )
		{
			if ( !referenceGraph.ContainsKey( entity ) )
				yield break;

			foreach ( var node in referenceGraph[ entity ] )
				yield return new Relation<ObjectNode, string>( entity, node, string.Empty );
		}

		public override GUIContent GetContent( ObjectNode entity )
		{
			return new GUIContent( entity.label, entity.label );
		}

		// draw nodes representing scene root GameObject in a special color
		public override Rect DrawContent( ObjectNode entity, EntityDrawContext drawContext )
		{
			if ( !entity.IsSceneObject )
				return DrawUtil.DrawContent( GetContent( entity ), drawContext );

			var colorBackup = drawContext.style.backgroundColor;
			drawContext.style.backgroundColor = sceneNodeColor;
			var rect = DrawUtil.DrawContent( GetContent( entity ), drawContext );
			drawContext.style.backgroundColor = colorBackup;
			return rect;
		}

		public override Rect OnGUI()
		{
			GUILayout.BeginHorizontal( EditorStyles.toolbar );
			{
				GUILayout.FlexibleSpace();

				searchString = BackendUtil.DrawEntitySelectSearchField( searchString, api );

				showLegend = GUILayout.Toggle( showLegend, "Legend", EditorStyles.toolbarButton, GUILayout.ExpandWidth( false ) );
				if ( showLegend )
				{
					string title = "Node colors";
					var colorLegend = new ColorLegendEntry[]
					{
						new ColorLegendEntry() { text = "Target Object", color = api.GetSkin().entityWidget.targetBackgroundColor },
						new ColorLegendEntry() { text = "Asset", color = api.GetSkin().entityWidget.backgroundColor },
						new ColorLegendEntry() { text = "Scene Object", color = sceneNodeColor }
					};

					var boxSize = ColorLegendBox.GetSize( title, colorLegend );
					float boxPosX = EditorGUIUtility.currentViewWidth - boxSize.x - 10;
					float boxPosY = 42;
					ColorLegendBox.Draw( new Rect( boxPosX, boxPosY, boxSize.x, boxSize.y ), title, colorLegend );
				}
			}
			GUILayout.EndHorizontal();

			return BackendUtil.GetMaxRect();
		}

		public override string GetEntityTooltip( ObjectNode entity )
		{
			return entity.tooltip;
		}

		public override void OnEntitySelectionChange( ObjectNode[] selection )
		{
			var single = selection.SingleOrDefault();
			if ( single != null && single.objs.Count() == 1 )
				Selection.activeObject = single.objs.First();
		}

	}
}
