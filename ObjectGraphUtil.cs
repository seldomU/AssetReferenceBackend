using UnityEngine;
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
            var graph = new ObjMap();
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

            var openNodes = cycleFree.Keys.ToHashSet();
            var toExpand = new Queue<Object>( new[] { graphRoot } );

            while ( toExpand.Any() )
            {
                var item = toExpand.Dequeue();
                openNodes.Remove( item );

                var successors = new HashSet<Object>();

                foreach ( var candidate in cycleFree[item].Except( new[] { item } ) )
                {
                    var testSets = openNodes.Except( new[] { candidate } );
                    if ( testSets.Any( x => cycleFree[ x ].Contains( candidate ) ) )
                        continue;

                    successors.Add( candidate );
                }

                graph[ item ] = successors;
                toExpand.Enqueue( successors );
            }

            return graph;
        }

        public static ObjMap GetCycleFreeDependencies( Object root, Object[] targets )
        {
            var connectedDeps = GetConnectedDependencies( root, targets );
            // replace cycles by a single objects
            // connectedDeps -> cycleFree


            var untested = connectedDeps.Keys.ToHashSet();
            var replacement = new Dictionary<Object, Object>();
            var replaceMap = new ObjMap();

            while ( untested.Any() )
            {
                var currentKey = untested.First();
                var currentValue = connectedDeps[ currentKey ];
                untested.Remove( currentKey );

                var identicals = untested
                    .Where( x => connectedDeps[ x ].SetEquals( currentValue ) )
                    .ToArray();

                if ( identicals.Any() )
                {
                    var cycleMembers = identicals.Concat( new[] { currentKey } );
                    var rep = CycleRep.Create( cycleMembers );
                    replaceMap[ rep ] = currentValue.Except( cycleMembers ).ToHashSet();

                    foreach ( var x in cycleMembers )
                        replacement[ x ] = rep;

                    foreach ( var x in identicals )
                        untested.Remove( x );
                }
            }

            System.Func<Object, Object> substitute = x => replacement.ContainsKey( x ) ? replacement[ x ] : x;

            var cycleFreeObjs = connectedDeps
                .Keys
                .Select( substitute )
                .ToHashSet();  // remove duplicates

            return cycleFreeObjs.ToDictionary(
                x => x,
                x => connectedDeps.ContainsKey( x ) ?
                    connectedDeps[ x ].Select( substitute ).ToHashSet() :
                    replaceMap[ x ].Select( substitute ).ToHashSet() );
        }

        public static ObjMap GetConnectedDependencies( Object root, Object[] targets )
        {
            var allDeps = GetAllDependencies( root );

            if ( !targets.Any() )
                return allDeps;

            // get the ones that depend on target
            var connectedToTarget = allDeps
                .Keys
                .Where( key => allDeps[ key ].Intersect(targets).Any() )
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
            var allRootDeps = GetDependencies( root );

            // get all of their dependencies
            return allRootDeps.ToDictionary( obj => obj, obj => GetDependencies( obj ) );
        }

        // returns all objects that obj references, plus itself
        static HashSet<Object> GetDependencies( Object obj )
        {
            return UnityEditor
                .EditorUtility
                .CollectDependencies( new[] { obj } )
                .ToHashSet();
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

        public static IEnumerable<T> GetRoots<T>( Dictionary<T,HashSet<T>>  map )
        {
            // return the nodes that are not referenced by any node (keys that are not values)
            return map.Keys.Where( k => !map.Values.Any( v => v.Contains( k ) ) );
        }
    }
}
