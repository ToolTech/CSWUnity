//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneObjectPool.cs
// Description	: Prototype-based GameObject pools for scene occurrences
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261103	Added capped occurrence pools with inactive prototypes, explicit prewarming and counters.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Transform = UnityEngine.Transform;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	/// <summary>Copied counters of one occurrence object pool since its scene was created.</summary>
	/// <remarks>
	/// Created counts GameObjects instantiated on demand because the pool was empty. Returned counts
	/// GameObjects kept for reuse and Destroyed counts GameObjects destroyed because the pool was full.
	/// </remarks>
	public readonly struct SceneObjectPoolDiagnostics
	{
		public int Free { get; }
		public int Capacity { get; }
		public long Prewarmed { get; }
		public long Created { get; }
		public long Reused { get; }
		public long Returned { get; }
		public long Destroyed { get; }

		internal SceneObjectPoolDiagnostics(int free, int capacity, long prewarmed, long created, long reused,
			long returned, long destroyed)
		{
			Free = free;
			Capacity = capacity;
			Prewarmed = prewarmed;
			Created = created;
			Reused = reused;
			Returned = returned;
			Destroyed = destroyed;
		}
	}

	/// <summary>Main-thread pool of inactive occurrence GameObjects cloned from one inactive prototype.</summary>
	/// <remarks>The prototype and free GameObjects stay beneath the borrowed, inactive pool parent.</remarks>
	internal sealed class SceneObjectPool
	{
		private readonly Stack<SceneInstanceHandle> _free;
		private readonly SceneInstanceHandle _prototype;
		private readonly Transform _parent;
		private readonly int _capacity;
		private long _prewarmed;
		private long _created;
		private long _reused;
		private long _returned;
		private long _destroyed;

		/// <summary>Creates the prototype; a renderable prototype carries a mesh filter and a disabled renderer.</summary>
		/// <remarks>One reference slot is reserved per GameObject of the cap, so returns never grow the pool.</remarks>
		internal SceneObjectPool(Transform parent, string name, int capacity, bool renderable)
		{
			_parent = parent;
			_capacity = Math.Max(0, capacity);
			_free = new Stack<SceneInstanceHandle>(_capacity);
			var prototype = new GameObject(name);
			prototype.SetActive(false);
			prototype.transform.SetParent(parent, false);
			_prototype = prototype.AddComponent<SceneInstanceHandle>();
			if (renderable)
			{
				prototype.AddComponent<MeshFilter>();
				prototype.AddComponent<MeshRenderer>().enabled = false;
			}
		}

		internal int Free => _free.Count;

		internal SceneObjectPoolDiagnostics Diagnostics => new SceneObjectPoolDiagnostics(
			_free.Count, _capacity, _prewarmed, _created, _reused, _returned, _destroyed);

		/// <summary>Takes an inactive GameObject, cloning the prototype only when the pool is empty.</summary>
		internal SceneInstanceHandle Acquire()
		{
			if (_free.Count != 0)
			{
				_reused++;
				return _free.Pop();
			}
			_created++;
			return Clone();
		}

		/// <summary>Adds one cloned GameObject when the pool holds fewer than the reserve and the cap.</summary>
		internal bool Prewarm(int reserve)
		{
			if (_free.Count >= Math.Min(reserve, _capacity))
				return false;
			_free.Push(Clone());
			_prewarmed++;
			return true;
		}

		/// <summary>Moves an inactive, detached GameObject beneath the pool and keeps it below the cap.</summary>
		internal void Return(SceneInstanceHandle handle)
		{
			handle.transform.SetParent(_parent, false);
			if (_free.Count < _capacity)
			{
				_free.Push(handle);
				_returned++;
			}
			else
			{
				_destroyed++;
				SceneObjects.Destroy(handle.gameObject);
			}
		}

		/// <summary>Forgets the free GameObjects; their owner destroys them with the pool parent.</summary>
		internal void Clear()
		{
			_free.Clear();
		}

		private SceneInstanceHandle Clone()
		{
			var handle = Object.Instantiate(_prototype, _parent, false);
			handle.TryGetComponent(out handle.Filter);
			handle.TryGetComponent(out handle.Renderer);
			return handle;
		}
	}
}
