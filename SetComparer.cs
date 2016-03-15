using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RelationsInspector.Backend.AssetDependency
{
	class SetComparer : IEqualityComparer<HashSet<Object>>
	{
		public bool Equals( HashSet<Object> a, HashSet<Object> b )
		{
			return a.SetEquals( b );
		}

		public int GetHashCode( HashSet<Object> a )
		{
			int hash = 17;
			foreach ( var item in a )
			{
				unchecked
				{
					hash = hash * 23 + item.GetHashCode();
				}
			}
			return hash;
		}
	}
}
