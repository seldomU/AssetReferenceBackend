using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using RelationsInspector.Extensions;

namespace RelationsInspector.Backend.AssetDependency
{
	using ObjMap = Dictionary<Object, HashSet<Object>>;

	class ObjectGraphUtil
	{
		public static ObjMap GetDependencyGraph( Object root, Object[] targets )
		{
			var cycleFree = GetCycleFreeDependencies( root, targets );
			if ( !cycleFree.Keys.Any() )
				return new ObjMap();

			// cycleFree -> graph
			var graphRoot = cycleFree.ContainsKey( root )
				?
				root
				:
				cycleFree
				.Keys
				.OfType<CycleRep>()
				.Where( x => x.members.Contains( root ) )
				.SingleOrDefault();

			if ( graphRoot == null )
			{
				// root object is not in the graph (using a scene as root obj does that)
				// add a representative for the scene object as new graph root
				graphRoot = CycleRep.Create( new[] { root } );
				graphRoot.name = root.name;
				var notSoMuchRoots = GetRoots( cycleFree );
				cycleFree[ graphRoot ] = notSoMuchRoots.ToHashSet();
			}

			// structure the dependencies
			return cycleFree.ToDictionary
				(
				pair => pair.Key,
				pair => pair.Value
					.Where( obj => obj != pair.Key && pair.Value.Except(new[] { obj }).Any(sibling=> cycleFree[ sibling].Contains( obj ) ) == false )
					.ToHashSet()
				);
		}

		public static ObjMap GetCycleFreeDependencies( Object root, Object[] targets )
		{
			var connectedDeps = GetConnectedDependencies( root, targets );
			// replace cycles by a single objects
			// connectedDeps -> cycleFree

			// group the map keys by value. 
			// that will create one group for each dependency-cycle, containing all its members
			var cycleGrouped = connectedDeps.Keys.GroupBy( key => connectedDeps[ key ], new SetComparer() );

			// create a representative object for each group and map it to the group members
			var repToMembers = cycleGrouped
				.Where( group => group.Count() > 1 )
				.ToDictionary( group => (Object)CycleRep.Create( group ), group => group.ToHashSet() );

			// map all keys to their representative
			var objToRep = new Dictionary<Object, Object>();
			foreach ( var pair in repToMembers )
			{
				foreach ( var obj in pair.Value )
					objToRep[ obj ] = pair.Key;
			}

			System.Func<Object, Object> newObj = ( obj ) => objToRep.ContainsKey( obj ) ? objToRep[ obj ] : obj;

			return cycleGrouped.ToDictionary
				( 
				group => newObj( group.First() ), 
				group => connectedDeps[ group.First() ]
					.Select( newObj )
					.Except( new[] { newObj( group.First() ) } )
					.ToHashSet() 
				);
		}

		public static ObjMap GetConnectedDependencies( Object root, Object[] targets )
		{
			var allDeps = GetAllDependencies( root );
			if ( !targets.Any() )
				return allDeps;

			// get the ones that depend on target
			var connectedToTarget = allDeps
				.Keys
				.Where( key => allDeps[ key ].Intersect( targets ).Any() )
				.ToHashSet();

			// map them to the subset of their dependencies that depends on target
			var connectedDeps = connectedToTarget
				.ToDictionary(
					obj => obj,
					obj => allDeps[ obj ].Intersect( connectedToTarget ).ToHashSet()
					);

			return connectedDeps;
		}

		public static ObjMap GetAllDependencies( Object root )
		{
			// get all objects that root depends on
			var rootDeps = GetDependencies( root );

			return rootDeps.ToDictionary( obj => obj, obj => GetDependencies( obj ).ToHashSet() );
		}

		// returns all objects that obj references, plus itself
		static IEnumerable<Object> GetDependencies( Object obj )
		{
			var assetGroups = EditorUtility.CollectDependencies( new[] { obj } )
				.Select( o => new { obj = o, path = AssetDatabase.GetAssetPath( o ) } )
				.Where( pair => !IgnoreDependencyFromPath( pair.path ) && !( pair.obj is Component ) )
				.GroupBy( pair => pair.path );

			foreach ( var group in assetGroups )
			{
				if ( string.IsNullOrEmpty( group.Key ) )
				{
					foreach ( var pair in group )
					{
						yield return pair.obj;
					}
				}
				else if ( group.Count() == 1 )
				{
					yield return group.First().obj;
				}
				else
				{
					yield return AssetDatabase.LoadAssetAtPath( group.Key, typeof( Object ) );
				}
			}
		}

		static bool IgnoreDependencyFromPath( string path )
		{
			return
				path.StartsWith( "Library" ) ||
				path.StartsWith( "Resources/unity_builtin_extra" ) ||
				path.EndsWith( "dll" );
		}

		// merges graphs a and b. treat cycleRep objects with equal members as identical
		public static ObjMap MergeGraphs( ObjMap a, ObjMap b )
		{
			var aReps = a.Keys.OfType<CycleRep>();
			var bReps = b.Keys.OfType<CycleRep>();

			// find matching cycle reps
			var matches = new Dictionary<Object, Object>();
			foreach ( var ar in aReps )
			{
				foreach ( var br in bReps )
				{
					if ( ar.EqualMembers( br ) )
					{
						matches[ ar ] = br;
						break;
					}
				}
			}

			System.Func<Object, Object> substitute = x => matches.ContainsKey( x ) ? matches[ x ] : x;

			var result = b.ToDictionary( pair => pair.Key, pair => pair.Value );
			foreach ( var x in a.Keys )
			{
				var addKey = substitute( x );
				var addValues = a[ x ].Select( v => substitute( v ) );
				if ( !result.ContainsKey( addKey ) )
					result[ addKey ] = new HashSet<Object>();

				foreach ( var v in addValues )
					result[ addKey ].Add( v );
			}

			return result;
		}

		public static IEnumerable<T> GetRoots<T>( Dictionary<T, HashSet<T>> map )
		{
			// return the nodes that are not referenced by any node other than themselves
			return map.Keys.Where( k => !map.Values.Except(new[] { map[k] } ).Any( v => v.Contains( k ) ) );
		}

		public static IEnumerable<T> GetAllNodes<T>( Dictionary<T, HashSet<T>> map )
		{
			return map.Values.SelectMany( o => o ).ToHashSet().Union( map.Keys );
		}
	}
}
