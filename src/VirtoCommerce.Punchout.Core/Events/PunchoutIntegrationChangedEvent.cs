using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Core.Events;

public class PunchoutIntegrationChangedEvent(IEnumerable<GenericChangedEntry<PunchoutIntegration>> changedEntries)
    : GenericChangedEntryEvent<PunchoutIntegration>(changedEntries);
