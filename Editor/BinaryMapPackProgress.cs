using System;

namespace Plugins.CarX.Modding.Creator.Editor
{
	/// <summary>
	/// Ход упаковки бинарной карты для <see cref="BinaryMapPacker"/>: переводит этапы, документы и перекодированные
	/// текстуры в общую долю 0..1 и синхронно передаёт её получателю на вызывающем потоке.
	/// </summary>
	/// <remarks>
	/// Доли: сбор файлов 0..0.02, преобразование документов и текстур 0.02..0.95, исходные изображения 0.95..0.97,
	/// запись и проверка архива 0.97..1. Дольше всего идёт перекодирование текстур, но сколько их будет, до чтения
	/// документов неизвестно, поэтому оценка - число изображений в staging-каталогах, а текстура весит
	/// <see cref="TextureWeight"/> документов. Шаг сообщается до работы с долей уже сделанных шагов. Если текстур выходит
	/// больше оценки, доля приближается к концу этапа, но не доходит до него. Доля никогда не убывает.
	/// Без получателя методы ничего не делают.
	/// </remarks>
	internal sealed class BinaryMapPackProgress
	{
		private const float CollectingEnd = 0.02f;
		private const float ConvertingEnd = 0.95f;
		private const float ImagesEnd = 0.97f;

		/// <summary>Перекодирование текстуры примерно на порядок дольше преобразования документа.</summary>
		private const int TextureWeight = 10;

		private readonly Action<string, float> m_onProgress;

		private int m_expectedWeight;
		private int m_doneWeight;
		private float m_lastFraction;

		/// <param name="onProgress">Получатель: описание шага на английском и общая доля 0..1; может быть null.</param>
		public BinaryMapPackProgress(Action<string, float> onProgress)
		{
			m_onProgress = onProgress;
		}

		public void ReportCollecting()
		{
			Report("Collecting staging files", fraction: 0f);
		}

		/// <param name="documentCount">Сколько документов (всё, кроме изображений) будет преобразовано.</param>
		/// <param name="imageCount">Сколько изображений в staging-каталогах: оценка числа перекодируемых текстур.</param>
		public void BeginConverting(int documentCount, int imageCount)
		{
			m_expectedWeight = documentCount + imageCount * TextureWeight;
			m_doneWeight = 0;
		}

		/// <summary>Начинается преобразование документа <paramref name="name"/>.</summary>
		public void ReportDocument(string name)
		{
			ReportStep("Converting ", name, weight: 1);
		}

		/// <summary>Начинается перекодирование текстуры; <paramref name="name"/> - что именно кодируется.</summary>
		public void ReportTexture(string name)
		{
			ReportStep("Encoding texture ", name, TextureWeight);
		}

		public void ReportImages()
		{
			Report("Copying original images", ConvertingEnd);
		}

		public void ReportWriting()
		{
			Report("Writing archive", ImagesEnd);
		}

		public void ReportPacked()
		{
			Report("Binary map packed", fraction: 1f);
		}

		private void ReportStep(string action, string name, int weight)
		{
			if (m_onProgress == null)
			{
				return;
			}

			// Знаменатель не меньше сделанного плюс шаг: превышение оценки не доводит долю до конца этапа.
			int total = Math.Max(m_expectedWeight, m_doneWeight + weight);
			float fraction = CollectingEnd + (ConvertingEnd - CollectingEnd) * m_doneWeight / total;
			m_doneWeight += weight;

			Report(action + name, fraction);
		}

		private void Report(string info, float fraction)
		{
			if (m_onProgress == null)
			{
				return;
			}

			// Оценка числа текстур грубая, поэтому доля только удерживается, а не откатывается.
			if (fraction < m_lastFraction)
			{
				fraction = m_lastFraction;
			}

			m_lastFraction = fraction;
			m_onProgress(info, fraction);
		}
	}
}
