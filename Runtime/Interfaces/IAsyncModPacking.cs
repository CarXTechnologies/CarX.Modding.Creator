using System;
using System.Threading;
using System.Threading.Tasks;

namespace Plugins.CarX.Modding.Creator.Runtime
{
	/// <summary>Провайдер, который дописывает собранные ресурсы асинхронно (с прогрессом и отменой).</summary>
	public interface IAsyncModPacking
	{
		Task EndPackingAsync(string catalog, object resource, Action<float> progress, CancellationToken cancellationToken);
	}
}
