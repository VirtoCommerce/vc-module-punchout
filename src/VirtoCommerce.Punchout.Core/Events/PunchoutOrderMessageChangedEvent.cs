using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Events;

public class PunchoutOrderMessageChangedEvent(IEnumerable<GenericChangedEntry<PunchoutOrderMessage>> changedEntries)
    : GenericChangedEntryEvent<PunchoutOrderMessage>(changedEntries);
