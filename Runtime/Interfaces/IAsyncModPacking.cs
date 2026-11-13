using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarX.Modding.Creator.Runtime
{
	/// <summary>Provider that writes collected resources asynchronously (with progress and cancellation).</summary>
	public interface IAsyncModPacking
	{
		Task EndPackingAsync(string catalog, object resource, Action<float> progress, CancellationToken cancellationToken);
	}
}
