using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CombatExtended
{
    public enum MeleeFallback
    {
        /// <summary>ToolCE picks MeleeReach based on AttackPartHeight</summary>
        Automatic,
        /// <summary>ToolCE only picked when it can reach through normal behaviour</summary>
        None,
        /// <summary>Attack upwards (towards feet) if the ToolCE reach doesn't intersect target hitbox</summary>
        NearestAbove,
        /// <summary>Attack downwards (towards head) if the ToolCE reach doesn't intersect target hitbox</summary>
        NearestBelow,
        /// <summary>Attack up or downwards (head/feet) if the ToolCE reach doesn't intersect target hitbox</summary>
        Nearest,
        /// <summary>Attack any region if ToolCE doesn't intersect hitbox</summary>
        FullBody
    }
}
