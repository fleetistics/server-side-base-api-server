using db_model.Notifications;
using exs.Database.Commons.Interfaces;
using mf5.CommonLogic.Utils;
using Microsoft.EntityFrameworkCore;

namespace api_server.Services.Notifications
{
	public class MessageTemplateProcessor
	{
		public interface IBuilder
		{
			IBuilder SetDetails(IDictionary<string, object?> details);
			string Title { get; }
			string Body { get; }
		}

		public MessageTemplateProcessor(LiquidTemplateProcessor liquidTemplateProcessor)
		{
			mLiquidTemplateProcessor = liquidTemplateProcessor;
		}

		public async Task<IBuilder> LoadAsync(int templateId, IRepository repository, CancellationToken cancellationToken)
		{
			var template = await repository.GetQueryable<MessageTemplate>(e => e.Id == templateId)
				.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
			if (template == null) throw new Exception("No tepmlate found #" + templateId);

			//await mLiquidTemplateProcessor.CheckIsInited(repository, cancellationToken);

			return new Builder(template, mLiquidTemplateProcessor);
		}

		private class Builder : IBuilder
		{
			public Builder(MessageTemplate template, LiquidTemplateProcessor liquidTemplateProcessor)
			{
				mTemplate = template;
				mLiquidTemplateProcessor = liquidTemplateProcessor;
			}

			public IBuilder SetDetails(IDictionary<string, object?> details)
			{
				mDetails = details;
				if (mProcessedTemplate != null)
				{
					mProcessedTemplate.Title = null;
					mProcessedTemplate.Body = null;
				}

				return this;
			}

			public string Title
			{
				get
				{
					if (string.IsNullOrEmpty(mTemplate.Title)) return "";
					if (mProcessedTemplate == null) mProcessedTemplate = new();
					if (mProcessedTemplate.Title == null)
					{
						mProcessedTemplate.Title = mLiquidTemplateProcessor.ProcessTemplate(mTemplate.Title, mDetails);
					}
					return mProcessedTemplate.Title ?? "";
				}
			}

			public string Body
			{
				get
				{
					if (string.IsNullOrEmpty(mTemplate.Body)) return "";
					if (mProcessedTemplate == null) mProcessedTemplate = new();
					if (mProcessedTemplate.Body == null)
					{
						mProcessedTemplate.Body = mLiquidTemplateProcessor.ProcessTemplate(mTemplate.Body, mDetails);
					}
					return mProcessedTemplate.Body ?? "";
				}
			}

			private IDictionary<string, object?>? mDetails;
			private MessageTemplate? mProcessedTemplate;

			private readonly MessageTemplate mTemplate;
			private readonly LiquidTemplateProcessor mLiquidTemplateProcessor;
		}


		private readonly LiquidTemplateProcessor mLiquidTemplateProcessor;
	}
}
