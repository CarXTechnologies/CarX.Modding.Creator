using System;

namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>Отчёт о ходе загрузки мода: этап, общая доля выполнения и байты текущего этапа.</summary>
	/// <remarks>
	/// <see cref="Fraction"/> описывает всю операцию, а не текущий этап, и в пределах одной операции не убывает,
	/// поэтому его можно сразу выводить в полосу прогресса. Единицу площадка сообщает только после успешного ответа.
	/// Байты относятся к текущему этапу и равны нулю, когда площадка их не сообщает (Steam, mod.io без CARX PATCH 2).
	/// </remarks>
	public readonly struct ModUploadProgress
	{
		public ModUploadStage Stage { get; }

		/// <summary>Общая доля выполнения всей операции, от 0 до 1.</summary>
		public float Fraction { get; }

		/// <summary>Сколько байт текущего этапа обработано; 0, если неизвестно.</summary>
		public long DoneBytes { get; }

		/// <summary>Сколько байт всего на текущем этапе; 0, если неизвестно.</summary>
		public long TotalBytes { get; }

		public ModUploadProgress(ModUploadStage stage, float fraction, long doneBytes, long totalBytes)
		{
			Stage = stage;
			Fraction = ClampFraction(fraction);
			DoneBytes = Math.Max(doneBytes, 0L);
			TotalBytes = Math.Max(totalBytes, 0L);
		}

		public override string ToString()
		{
			return TotalBytes > 0
				? $"{Stage} {Fraction:P0} ({DoneBytes}/{TotalBytes} bytes)"
				: $"{Stage} {Fraction:P0}";
		}

		/// <summary>Приводит долю к 0..1; NaN считается нулём, чтобы полоса не получила мусор от площадки.</summary>
		private static float ClampFraction(float fraction)
		{
			if (!(fraction > 0f))
			{
				return 0f;
			}

			return fraction < 1f ? fraction : 1f;
		}
	}
}
