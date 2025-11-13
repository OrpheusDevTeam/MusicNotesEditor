using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Models.Config
{
    public class AppSettings
    {
        [Required]
        public int? SnappingThreshold { get; set; }

        [Required]
        public int? AdditionalStaffLines { get; set; }

        [Required] 
        public int? StaffDistance { get; set; }

        [Required] 
        public int? AdditionalSystemDistance { get; set; }

        [Required] 
        public int? DefaultInitialMeasures { get; set; }

        [Required] 
        public int? MinimalInitialMeasurePerStaff { get; set; }
    }
}
