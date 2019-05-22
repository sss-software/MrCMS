using System.Collections.Generic;
using MrCMS.Messages;

namespace MrCMS.Web.Apps.Admin.Services
{
    public interface IMessageTemplateTokensAdminService
    {
        List<string> GetTokens(MessageTemplate messageTemplate);
    }
}