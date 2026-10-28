using FIBER.Source.Models.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FIBER.Source.Models.Joins
{
    public class MediaAttachmentLink
    {
        public int MediaAttachmentId { get; set; }
        public MediaOwnerType OwnerType { get; set; }
        public int OwnerId { get; set; }

        public MediaAttachment MediaAttachment { get; set; } = null!;
    }
}
