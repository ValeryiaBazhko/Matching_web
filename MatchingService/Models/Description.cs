using System.ComponentModel.DataAnnotations;

namespace MatchingApp.Models
{
    public class Description
    {
        public int Id { get; set; }
        
        [Required]
        public string Content { get; set; } = string.Empty;
    }
}