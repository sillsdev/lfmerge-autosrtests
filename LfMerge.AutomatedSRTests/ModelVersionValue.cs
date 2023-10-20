// Copyright (c) 2018 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System.Collections;

namespace LfMerge.AutomatedSRTests
{
	public static class ModelVersionValue
	{
		/// <summary>
		/// Return all supported model versions
		/// </summary>
		public static IEnumerable GetValues()
		{
			for (var modelVersion = Settings.MinModelVersion;
				modelVersion <= Settings.MaxModelVersion;
				modelVersion++)
			{
				if (modelVersion == 7000071)
					continue;

				yield return modelVersion;
			}
		}
	}
}