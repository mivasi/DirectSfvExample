using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Forge.DesignAutomation.Model;

namespace Viewer.Models
{
    public class WorkItemWithStatus
    {
        public WorkItem WorkItem { get; set; }
        public WorkItemStatus Status { get; set; }

        public WorkItemWithStatus(WorkItem workItem, WorkItemStatus status)
        {
            WorkItem = workItem;
            Status = status;
        }
    }
}
