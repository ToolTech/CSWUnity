//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: Factory.cs
// Description	: Registry and base class of the Unity realization of GizmoSDK nodes
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Created CSW.Unity.Factory after cswFactory in CSWUnreal.	(2.12.327)
// AMO	261105	Carried the session's loader-thread mesh preparations in the capture context.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CSW.Unity.StreamingMap;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using UnityEngine;

namespace CSW.Unity
{
	/// <summary>Realizes the occurrences of a GizmoSDK node type as Unity objects, after cswFactory in CSWUnreal.</summary>
	/// <remarks>
	/// Factories are registered by GizmoSDK type name, such as <c>gzGeometry</c>. An occurrence is realized by
	/// the factory of the most specific type of its node that has one. The built-in factories realize
	/// <c>gzNode</c>, <c>gzGroup</c>, <c>gzTransform</c>, <c>gzRoiNode</c>, <c>gzGeometry</c>, <c>gzLod</c>,
	/// <c>gzCrossboard</c> and <c>gzDynamicLoader</c>. An application realizes a type in its own way by deriving
	/// from the built-in factory of the type, overriding the application hooks and registering it in place of
	/// that factory, or by registering it for a more specific type.
	/// </remarks>
	public abstract class Factory
	{
		private static readonly object _lock = new object();
		private static readonly Dictionary<string, Factory> _factories = new Dictionary<string, Factory>();
		private static readonly Dictionary<IntPtr, Factory> _selected = new Dictionary<IntPtr, Factory>();

		static Factory()
		{
			var node = new NodeFactory();
			Add("gzNode", node);
			Add("gzGroup", node);
			Add("gzTransform", new TransformFactory());
			Add("gzRoiNode", new RoiNodeFactory());
			Add("gzGeometry", new GeometryFactory());
			Add("gzLod", new LodFactory());
			Add("gzCrossboard", new CrossboardFactory());
			Add("gzDynamicLoader", new DynamicLoaderFactory());
		}

		/// <summary>Registers a factory for a GizmoSDK type name.</summary>
		/// <returns>False, with a warning message, when the type already has a factory.</returns>
		/// <remarks>Unregister the current factory first to replace it. Applies to occurrences captured afterwards.</remarks>
		public static bool Register(string typeName, Factory factory)
		{
			if (string.IsNullOrEmpty(typeName))
				throw new ArgumentException("A factory needs a GizmoSDK type name.", nameof(typeName));
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));
			lock (_lock)
			{
				if (_factories.ContainsKey(typeName))
				{
					Platform.SendMessage(MessageLevel.WARNING, $"Multiple CSW.Unity.Factory registrations for type ({typeName})");
					return false;
				}
				Add(typeName, factory);
				return true;
			}
		}

		/// <summary>Removes the factory of a GizmoSDK type name.</summary>
		/// <returns>False, with a warning message, when the type has no factory.</returns>
		public static bool Unregister(string typeName)
		{
			lock (_lock)
			{
				if (typeName == null || !_factories.Remove(typeName))
				{
					Platform.SendMessage(MessageLevel.WARNING, $"No CSW.Unity.Factory registered for type ({typeName})");
					return false;
				}
				_selected.Clear();
				return true;
			}
		}

		/// <summary>Returns the factory registered for exactly this GizmoSDK type name, or null.</summary>
		public static Factory Get(string typeName)
		{
			lock (_lock)
				return typeName != null && _factories.TryGetValue(typeName, out var factory) ? factory : null;
		}

		/// <summary>Returns the factory of the most specific GizmoSDK type of the node that has one, or null.</summary>
		public static Factory Select(Node node)
		{
			if (node == null)
				throw new ArgumentNullException(nameof(node));
			var type = Reference_getType(node.GetNativeReference());
			lock (_lock)
			{
				if (_selected.TryGetValue(type, out var selected))
					return selected;
				for (var current = type; current != IntPtr.Zero; current = Reference_getParentType(current))
				{
					if (_factories.TryGetValue(Marshal.PtrToStringUni(Reference_getTypeName(current)), out selected))
						break;
				}
				// Native types are static, so the selection is kept until the registry changes.
				_selected.Add(type, selected);
				return selected;
			}
		}

		private static void Add(string typeName, Factory factory)
		{
			_factories.Add(typeName, factory);
			_selected.Clear();
		}

		/// <summary>Captures application data from the node of an occurrence.</summary>
		/// <remarks>
		/// Called on the Unity main thread while CSWUnity holds edit access for a command buffer, for each new
		/// and updated occurrence, but not for transform-only updates. Do not create or change Unity objects and
		/// do not keep the borrowed node and state, which are released after the capture.
		/// </remarks>
		/// <returns>Data passed to <see cref="Construct"/> or <see cref="Update"/>.</returns>
		protected virtual object Capture(Node node, State state)
		{
			return null;
		}

		/// <summary>Constructs the application part of a new occurrence after edit access is released.</summary>
		/// <remarks>The object stays presentation-owned and is pooled; <see cref="Destroy"/> must undo this.</remarks>
		protected virtual void Construct(GameObject gameObject, object data)
		{
		}

		/// <summary>Updates the application part of an occurrence after edit access is released.</summary>
		protected virtual void Update(GameObject gameObject, object data)
		{
		}

		/// <summary>Removes the application part of an occurrence before its object returns to the pool.</summary>
		/// <param name="data">The data of the latest capture.</param>
		protected virtual void Destroy(GameObject gameObject, object data)
		{
		}

		internal void CaptureOccurrence(FactoryCapture capture)
		{
			CaptureNode(capture);
			capture.Data.Factory = this;
			capture.Data.FactoryData = Capture(capture.Node, capture.State);
		}

		internal void ConstructOccurrence(GameObject gameObject, object data)
		{
			Construct(gameObject, data);
		}

		internal void UpdateOccurrence(GameObject gameObject, object data)
		{
			Update(gameObject, data);
		}

		internal void DestroyOccurrence(GameObject gameObject, object data)
		{
			Destroy(gameObject, data);
		}

		/// <summary>Captures the realization of the built-in factories; the base realizes a group.</summary>
		internal virtual void CaptureNode(FactoryCapture capture)
		{
			var node = capture.Node;
			var data = capture.Data;
			var parent = capture.Parent;
			var mask = node.IntersectMask;
			var name = node.Name;
			data.Name = string.IsNullOrEmpty(name) ? node.GetNativeTypeName() : name;
			data.IntersectMask = mask == IntersectMaskValue.NOTHING
				? parent.IntersectMask
				: parent.IntersectMask & mask;
			data.IsStatic = parent.IsStatic || node is RefNode;
			data.BoundaryCenter = node.BoundaryCenter.CopyVector3();
			data.Kind = SceneNodeKind.Group;
		}

		[DllImport(GizmoSDK.GizmoBase.Platform.BRIDGE, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr Reference_getType(IntPtr reference);
		[DllImport(GizmoSDK.GizmoBase.Platform.BRIDGE, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr Reference_getParentType(IntPtr type);
		[DllImport(GizmoSDK.GizmoBase.Platform.BRIDGE, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr Reference_getTypeName(IntPtr type);
	}

	/// <summary>Inputs of a capture under edit access, reused for each occurrence.</summary>
	internal sealed class FactoryCapture
	{
		internal Node Node;
		internal State State;
		internal bool IsUpdate;
		internal SceneInstanceHandle Parent;
		internal SceneResources Resources;
		internal PreparedMeshCache PreparedMeshes;
		internal IReadOnlyList<GeometryBuilderBase> Builders;
		internal SceneNodeData Data;

		internal void Clear()
		{
			Node = null;
			State = null;
			Parent = null;
			Data = null;
		}
	}
}
