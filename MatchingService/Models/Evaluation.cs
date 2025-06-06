using System.ComponentModel.DataAnnotations;

namespace MatchingApp.Models
{
    public class Evaluation
    {
        public int Id { get; set; }
        
        [Required]
        public Guid SessionId { get; set; }
        
        public int Description1Id { get; set; }
        public Description Description1 { get; set; } = null!;
        
        public int Description2Id { get; set; }
        public Description Description2 { get; set; } = null!;
        
        public bool IsMatch { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}