using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using RelationsInspector.Extensions;

namespace RelationsInspector.Backend.AssetDependency
{
	using ObjMap = Dictionary<Object, HashSet<Object>>;
	using ObjNodeGraph = Dictionary<ObjectNode, HashSet<ObjectNode>>;

	public static class ObjectDependencyUtil
	{
		public static ObjNodeGraph GetReferenceGraph( string sceneFilePath, HashSet<Object> targets )
		{
			// get the scene's objects
			var sceneObjects = UnityEditorInternal
				.InternalEditorUtility
				.LoadSerializedFileAndForget( sceneFilePath )
				.ToHashSet();

			// get the root gameObjects
			var rootGOs = sceneObjects
				.OfType<GameObject>()
				.Where( go => go.transform.parent == null );

			// build the Object graph
			var objGraph = new ObjMap();
			var targetArray = targets.ToArray();
			foreach ( var rootGO in rootGOs )
			{
				var rootGOgraph = ObjectGraphUtil.GetDependencyGraph( rootGO, targetArray );
				objGraph = ObjectGraphUtil.MergeGraphs( objGraph, rootGOgraph );
			}

			// convert it to a SceneObjectNode graph, so we can destroy the objects
			string fileName = System.IO.Path.GetFileName( sceneFilePath );
			var nodeGraph = ObjectGraphToObjectNodeGraph( objGraph, obj => GetSceneObjectNode( obj, targets, sceneObjects, sceneFilePath ) );

			// destroy the scene Objects
			var sceneObjArray = sceneObjects.ToArray();
			for ( int i = 0; i < sceneObjArray.Length; i++ )
				Object.DestroyImmediate( sceneObjArray[ i ] );
			System.GC.Collect();

			return nodeGraph;
		}

		public static ObjNodeGraph GetActiveSceneReferenceGraph( HashSet<Object> targets )
		{
			var rootGameObjects = ActiveSceneRootGameObjects();

			// build the Object graph
			var objGraph = new ObjMap();
			var targetArray = targets.ToArray();
			foreach ( var rootGO in rootGameObjects )
			{
				var rootGOgraph = ObjectGraphUtil.GetDependencyGraph( rootGO, targetArray );
				objGraph = ObjectGraphUtil.MergeGraphs( objGraph, rootGOgraph );
			}

			// convert it to a SceneObjectNode graph, so we can destroy the objects
			return ObjectGraphToObjectNodeGraph( objGraph, obj => GetActiveSceneObjectNode( obj, targets ) );
		}

		public static IEnumerable<GameObject> ActiveSceneRootGameObjects()
		{
			var prop = new HierarchyProperty( HierarchyType.GameObjects );
			var expanded = new int[ 0 ];
			while ( prop.Next( expanded ) )
			{
				yield return prop.pptrValue as GameObject;
			}
		}

		// turn object graph into VisualNode graph (mapping obj -> name)
		static ObjNodeGraph ObjectGraphToObjectNodeGraph( ObjMap objGraph, System.Func<Object, ObjectNode> getNode )
		{
			var referencedObjects = objGraph.Values.SelectMany( o => o ).ToHashSet();

			// get all graph objects
			var allObjs = objGraph.Keys.Concat( referencedObjects ).ToHashSet();

			// map them to VisualNodes
			var objToNode = allObjs.ToDictionary( obj => obj, obj => getNode( obj ) );

			// convert from Object to SceneObjectNode and flip the edge direction
			return referencedObjects.ToDictionary(
				x => objToNode[ x ],
				x => objGraph
					.Where( pair => pair.Value.Contains( x ) )
					.Select( pair => objToNode[ pair.Key ] )
					.ToHashSet()
				);
		}

		static ObjectNode GetActiveSceneObjectNode( Object obj, HashSet<Object> targets )
		{
			string label = obj.name;
			string tooltip = "";
			bool isSceneObj = false;
			Object[] objects;

			string sceneName = EditorApplication.currentScene.Split( '/' ).Last();

			var asCycleRep = obj as CycleRep;
			if ( asCycleRep != null )
			{
				label = asCycleRep.name;
				if ( targets.Intersect( asCycleRep.members ).Any() )
					label += "\nScene " + sceneName;
				tooltip = !string.IsNullOrEmpty( label ) ? label : string.Join( "\n", asCycleRep.members.Select( m => m.name ).ToArray() );

				// we consider rep as a scene Obj if all its members are scene objs
				isSceneObj = asCycleRep.members.All( m => IsSceneObject( m ) );
				objects = asCycleRep.gameObject != null ? new[] { asCycleRep.gameObject } : asCycleRep.members.ToArray();
			}
			else
			{
				// add scene name. if label has content, put the scene name in a new line
				if ( targets.Contains( obj ) )
					label += ( ( label == "" ) ? "" : "\n" ) + "Scene " + sceneName;

				isSceneObj = IsSceneObject( obj );
				objects = new[] { obj };
			}

			if ( isSceneObj )
				objects = new Object[] { }; // todo: get scene object

			return new ObjectNode( label, tooltip, objects, isSceneObj );
		}

		// return true if obj is part of a scene
		static bool IsSceneObject( Object obj )
		{
			// scene objects have no asset path
			return string.IsNullOrEmpty( AssetDatabase.GetAssetPath( obj ) );
		}

		static ObjectNode GetSceneObjectNode( Object obj, HashSet<Object> targets, HashSet<Object> sceneObjects, string scenePath )
		{
			string label = obj.name;
			string tooltip = "";
			bool isSceneObj = false;
			Object[] objects;

			string sceneName = System.IO.Path.GetFileNameWithoutExtension( scenePath );

			var asCycleRep = obj as CycleRep;
			if ( asCycleRep != null )
			{
				label = asCycleRep.name;
				if ( targets.Intersect( asCycleRep.members ).Any() )
					label += "\nScene " + sceneName;
				tooltip = !string.IsNullOrEmpty( label ) ? label : string.Join( "\n", asCycleRep.members.Select( m => m.name ).ToArray() );

				// we consider rep as a scene Obj if all its members are scene objs
				isSceneObj = !asCycleRep.members.Except( sceneObjects ).Any();
				objects = asCycleRep.gameObject != null ? new[] { asCycleRep.gameObject } : asCycleRep.members.ToArray();
			}
			else
			{
				// add scene name. if label has content, put the scene name in a new line
				if ( targets.Contains( obj ) )
					label += ((label == "") ? "" : "\n") + "Scene " + sceneName;

				isSceneObj = sceneObjects.Contains( obj );
				objects = new[] { obj };
			}

			if ( isSceneObj )
				objects = new Object[] { }; // todo: get scene object

			return new ObjectNode( label, tooltip, objects, isSceneObj );
		}

		// returns true if the object is a prefab
		static bool IsPrefab( Object obj )
		{
			return PrefabUtility.GetPrefabParent( obj ) == null && PrefabUtility.GetPrefabObject( obj ) != null;
		}

		// merge two graphs
		public static void AddGraph<T>( Dictionary<T, HashSet<T>> graph, Dictionary<T, HashSet<T>> addedGraph ) where T : class
		{
			foreach ( var pair in addedGraph )
			{
				if ( !graph.ContainsKey( pair.Key ) )
					graph[ pair.Key ] = pair.Value;
				else
					graph[ pair.Key ].UnionWith( pair.Value );
			}
		}
	}
}
