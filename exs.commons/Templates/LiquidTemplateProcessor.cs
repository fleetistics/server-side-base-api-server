using exs.commons.Templates;

namespace mf5.CommonLogic.Utils
{
    public class LiquidTemplateProcessor
    {
        public LiquidTemplateProcessor() 
        {
        }

		public string ProcessTemplate(string? template, IDictionary<string, object?>? details)
        {
            if (string.IsNullOrEmpty(template)) return "";
			if (details == null || details.Count == 0) return template;
			checkInited();
            return LiquidTemplate.Process(template, details);
        }

        private void checkInited()
		{
			if (!mInited)
			{
				lock (mInitLock)
				{
					if (!mInited)
					{
						LiquidTemplate.Init();
						mInited = true;
					}
				}
			}
		}

		private bool mInited = false;
        private readonly object mInitLock = new object();
	}
}
