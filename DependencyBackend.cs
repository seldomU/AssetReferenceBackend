using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;


namespace RelationsInspector.Backend.AssetDependency
{
	using ObjMap = Dictionary<Object, HashSet<Object>>;

	[Title( "Object dependencies" )]
	[Version( "1.0.0" )]
	[Description( "Shows all objects that a target object depends on, including the dependencies between those." )]
	[Documentation( @"https://seldomu.github.io/riBackends/ObjectDependency/" )]
	class DependencyBackend : MinimalBackend<Object, string>
	{
		ObjMap graph;
		string searchString;

		public override void Awake( GetAPI getAPI )
		{
			graph = new ObjMap();
			base.Awake( getAPI );
		}

		public override IEnumerable<Object> Init( object target )
		{
			var targetObj = target as Object;
			// generate the dependency graph for this target
			var rootGOgraph = ObjectGraphUtil.GetDependencyGraph( targetObj, new Object[ 0 ] );
			// merge it with the existing graphs (of other targets)
			graph = ObjectGraphUtil.MergeGraphs( graph, rootGOgraph );

			System.GC.Collect();

			// targets have probably been substituted by cycleReps in the graph, so just use all parent-less nodes as seeds
			return ObjectGraphUtil.GetRoots( rootGOgraph );
		}

		public override IEnumerable<Relation<Object, string>> GetRelations( Object entity )
		{
			if ( !graph.ContainsKey( entity ) )
				yield break;

			foreach ( var node in graph[ entity ] )
				yield return new Relation<Object, string>( entity, node, string.Empty );
		}

		public override string GetEntityTooltip( Object entity )
		{
			var asCycleRep = entity as CycleRep;
			if ( asCycleRep == null )
				return base.GetEntityTooltip( entity );

			if ( asCycleRep.gameObject == null )
				return string.Join( "\n", asCycleRep.members.Select( x => x.name ).ToArray() );

			return asCycleRep.gameObject.name;
		}

		public override void OnEntitySelectionChange( Object[] selection )
		{
			// replaces gameobject-cycleReps by their gameObjects
			System.Func<Object, Object> substitute = x =>
			 {
				 var asRep = x as CycleRep;
				 if ( asRep == null )
					 return x;

				 if ( asRep.members.Count() == 1 )
					 return asRep.members.First();

				 if ( asRep.gameObject != null )
					 return asRep.gameObject;

				 return asRep;
			 };

			Selection.objects = selection.Select( substitute ).ToArray();
		}

		public override Rect OnGUI()
		{
			GUILayout.BeginHorizontal( EditorStyles.toolbar );
			{
				GUILayout.FlexibleSpace();
				searchString = BackendUtil.DrawEntitySelectSearchField( searchString, api );
			}
			GUILayout.EndHorizontal();
			return BackendUtil.GetMaxRect();
		}

		public override void OnDestroy()
		{
			if ( graph != null )
			{
				// destroy all cycleReps
				var cycleReps = ObjectGraphUtil.GetAllNodes( graph ).OfType<CycleRep>().ToArray();
				foreach ( var cycleRep in cycleReps )
					Object.DestroyImmediate( cycleRep );
			}
		}
	}
}
