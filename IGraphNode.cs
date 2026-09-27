using Godot;
using System;

/// <summary>
/// Interface for nodes in the visual graph that can pass data between each other.
/// Implements the visitor pattern to pass data through connections in _Process.
/// </summary>
public interface IGraphNode
{
	/// <summary>
	/// Get the output data for a specific output slot.
	/// </summary>
	/// <param name="outputSlot">The output slot index</param>
	/// <returns>The data to pass to connected nodes</returns>
	Variant GetOutputData(int outputSlot);

	/// <summary>
	/// Receive input data from a connected node on a specific input slot.
	/// </summary>
	/// <param name="inputSlot">The input slot index</param>
	/// <param name="data">The data received from the connected node</param>
	void SetInputData(int inputSlot, Variant data);
}

/// <summary>
/// Implemented by graph nodes that render into a SubViewport and need to match the output resolution.
/// </summary>
public interface IResolutionDependent
{
	void SetResolution(Vector2I resolution);
}
