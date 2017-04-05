using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dematic.DATCOMAUS
{
    public interface IDATCOMAUSTelegrams
    {
        string PLCIdentifier { get; }
        string VFCIdentifier { get; }
        TelegramTemplate Template { get; }

        int GetTelegramLength(TelegramTypes telegramType);
    }
}
