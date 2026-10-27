//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneCommandInbox.cs
// Description	: Bounded ordered ownership transfer from native callbacks
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added bounded owned-buffer handoff and native session lifetime handling.	(2.12.327)
// AMO	261023	Queued value-only frame controls with one trigger and no graph-lock wait.	(2.12.327)
// AMO	261026	Included Omni mode in frame input and versioned inbox notifications for optional frame waits.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using System.Threading;
using CSW;
using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	internal sealed class SceneCommandInbox
	{
		private readonly object _gate = new object();
		private readonly Queue<(CommandBuffer Buffer, uint Entries)> _buffers =
			new Queue<(CommandBuffer, uint)>();
		private readonly Action<CommandBuffer> _release;
		private readonly int _maximumBuffers;
		private readonly uint _maximumCommands;
		private bool _accepting = true;
		private uint _commands;
		private string _fault;
		private string _releaseFault;
		private long _activityVersion;

		internal SceneCommandInbox(int maximumBuffers = 256, uint maximumCommands = 262144,
			Action<CommandBuffer> release = null)
		{
			if (maximumBuffers < 1 || maximumCommands < 1)
				throw new ArgumentOutOfRangeException(nameof(maximumBuffers));
			_maximumBuffers = maximumBuffers;
			_maximumCommands = maximumCommands;
			_release = release ?? (buffer => buffer.Dispose());
		}

		internal string Fault
		{
			get { lock (_gate) return _releaseFault == null ? _fault : $"{_fault}\n{_releaseFault}"; }
		}

		internal int PendingBuffers
		{
			get { lock (_gate) return _buffers.Count; }
		}

		internal long ActivityVersion
		{
			get { lock (_gate) return _activityVersion; }
		}

		internal void WaitForActivity(long observedVersion, int milliseconds)
		{
			lock (_gate)
				if (_accepting && _activityVersion == observedVersion)
					Monitor.Wait(_gate, milliseconds);
		}

		// The callback transfers this exact owned wrapper. Its graph lock is borrowed.
		internal void Receive(CommandReceiver receiver, CSW.SceneManager manager, CommandBuffer buffer)
		{
			if (buffer == null)
			{
				RecordFault("SceneManager delivered a null command buffer.");
				return;
			}

			var accepted = false;
			try
			{
				buffer.BufferDeleteMode = CommandBuffer.CommandBufferDeleteMode.EDIT_LOCK;
				var entries = buffer.Entries;
				lock (_gate)
				{
					if (_accepting &&
						_buffers.Count < _maximumBuffers &&
						entries <= _maximumCommands - _commands)
					{
						_buffers.Enqueue((buffer, entries));
						_commands += entries;
						accepted = true;
					}
					else if (_accepting)
					{
						_fault = "SceneManager command inbox overflow; the session must be restarted.";
						_accepting = false;
					}
					_activityVersion++;
					Monitor.PulseAll(_gate);
				}
			}
			catch (Exception exception)
			{
				RecordFault($"SceneManager callback failed: {exception}");
			}
			finally
			{
				if (!accepted)
				{
					try
					{
						_release(buffer);
					}
					catch (Exception exception)
					{
						RecordFault($"SceneManager callback release failed: {exception}", true);
						lock (_gate) _buffers.Enqueue((buffer, 0));
					}
				}
			}
		}

		private void RecordFault(string message, bool release = false)
		{
			lock (_gate)
			{
				const int maximumFaultLength = 8192;
				if (message.Length > maximumFaultLength)
					message = message.Substring(0, maximumFaultLength);
				if (release)
					_releaseFault ??= message;
				else
					_fault ??= message;
				_accepting = false;
				_activityVersion++;
				Monitor.PulseAll(_gate);
			}
		}

		internal bool TryDequeue(out CommandBuffer buffer)
		{
			lock (_gate)
			{
				if (_buffers.Count == 0)
				{
					buffer = null;
					return false;
				}
				var entry = _buffers.Dequeue();
				buffer = entry.Buffer;
				_commands -= entry.Entries;
				return true;
			}
		}

		internal void StopAccepting()
		{
			lock (_gate)
			{
				_accepting = false;
				_activityVersion++;
				Monitor.PulseAll(_gate);
			}
		}

		internal void DrainAlreadyLocked()
		{
			var cleanup = new SceneCleanup();
			var failed = new List<CommandBuffer>();
			while (TryDequeue(out var buffer))
			{
				var owned = buffer;
				cleanup.Attempt(() =>
				{
					try { _release(owned); }
					catch
					{
						failed.Add(owned);
						throw;
					}
				});
			}
			lock (_gate)
				foreach (var buffer in failed)
					_buffers.Enqueue((buffer, 0));
			cleanup.Complete("Could not release every queued SceneManager buffer.");
		}
	}

	internal sealed class SceneManagerSession : IDisposable
	{
		private CSW.SceneManager _manager;
		private CommandReceiver _receiver;
		private bool _disposed;

		internal SceneCommandInbox Inbox { get; } = new SceneCommandInbox();
		internal bool IsDisposed => _disposed;
		internal bool HasPendingCommands => _manager?.HasPendingCommands ?? false;
		internal bool IsRunning => _manager != null && _manager.IsRunning();

		internal SceneManagerSession(uint loaders)
		{
			try
			{
				NodeLock.WaitLockEdit();
				try
				{
					_manager = new CSW.SceneManager();
					_receiver = new CommandReceiver();
					_receiver.OnCommand += Inbox.Receive;
					_manager.AddCommandReceiver(_receiver);
					Submit(new SceneCommandInitialize(false));
					Submit(new SceneCommandSetLoaders(loaders));
				}
				finally
				{
					NodeLock.UnLock();
				}
				if (!_manager.Run(true))
					throw new InvalidOperationException("The native SceneManager worker could not start.");
			}
			catch
			{
				Dispose();
				throw;
			}
		}

		internal uint Submit(SceneCommand command)
		{
			// These control inputs contain values only; native applies them on its worker.
			try
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(SceneManagerSession));
				var id = command.ID;
				_manager.AddSingleCommand(command);
				return id;
			}
			finally
			{
				command.Dispose();
			}
		}

		internal uint SubmitFrame(CameraRelativeFrame camera, bool omniView = false)
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(SceneManagerSession));
			using (var buffer = new CommandBuffer())
			{
				var refresh = AppendFrameInput(buffer, camera, omniView);
				_manager.AddCommandBuffer(buffer, true);
				return refresh;
			}
		}

		internal static uint AppendFrameInput(CommandBuffer buffer, CameraRelativeFrame camera, bool omniView = false)
		{
			AppendInput(buffer, new SceneCommandSetOmniTraverse(omniView));
			AppendInput(buffer, new SceneCommandPositionCamera(
				camera.GlobalPosition, camera.Hpr, camera.HorizontalFieldOfView, camera.VerticalFieldOfView));
			AppendInput(buffer, new SceneCommandCameraSettings(camera.NearDistance, camera.FarDistance));
			AppendInput(buffer, new SceneCommandSetLodFactor(camera.LodFactor));
			return AppendInput(buffer, new SceneCommandRefreshScene(
				camera.RenderTime, camera.Width, camera.Height, camera.Width));
		}

		private static uint AppendInput(CommandBuffer buffer, SceneCommand command)
		{
			try
			{
				buffer.AddCommand(command);
				return command.ID;
			}
			finally
			{
				command.Dispose();
			}
		}

		public void Dispose()
		{
			if (_disposed)
				return;
			Inbox.StopAccepting();

			// Never wait for the worker or loader callbacks with the graph edit lock held.
			if (_manager != null)
				_manager.Shutdown();

			var cleanup = new SceneCleanup();
			NodeLock.WaitLockEdit();
			try
			{
				cleanup.Attempt(() =>
				{
					if (_receiver != null)
					{
						_receiver.OnCommand -= Inbox.Receive;
						_receiver.Dispose();
						_receiver = null;
					}
				});
				cleanup.Attempt(Inbox.DrainAlreadyLocked);
			}
			finally
			{
				NodeLock.UnLock();
			}
			cleanup.Attempt(() =>
			{
				_manager?.Dispose();
				_manager = null;
			});
			_disposed = _receiver == null && _manager == null && Inbox.PendingBuffers == 0;
			cleanup.Complete("SceneManager session cleanup failed.");
		}
	}
}
