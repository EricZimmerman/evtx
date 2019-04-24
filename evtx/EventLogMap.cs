using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;

namespace evtx
{
   public class EventLogMap
    {
        public string Author { get; set; }
        public int EventId { get; set; }
        public string Description { get; set; }

        public List<MapEntry> Maps { get; set; }

        public override string ToString()
        {
            return
                $"EventId: {EventId} Author: {Author} Description: {Description}, Map count: {Maps.Count:N0}";
        }
    }

   public class MapEntry
   {
        public string Property { get; set; }
        public string PropertyValue { get; set; }

        public List<ValueEntry> Values { get; set; }

        public override string ToString()
        {
            return
                $"Author: {Property} Description: {PropertyValue}, Values count: {Values.Count:N0}";
        }
   }

   public class ValueEntry
   {
       public string Name { get; set; }
       public string Value { get; set; }

       public override string ToString()
       {
           return $"Name: {Name} Value: {Value}";
       }
   }

   public class ValueEntryValidator : AbstractValidator<ValueEntry>
   {
       public ValueEntryValidator()
       {
           RuleFor(target => target.Name).NotEmpty();
           RuleFor(target => target.Value).NotEmpty();
       }
   }

   public class MapEntryValidator : AbstractValidator<MapEntry>
   {
       public MapEntryValidator()
       {
           RuleFor(target => target.Property).NotEmpty();
           RuleFor(target => target.PropertyValue).NotEmpty();

           RuleFor(target => target.Values.Count).GreaterThan(0).When(t => t.Values != null);

           RuleForEach(target => target.Values).NotNull()
               .WithMessage(
                   "Values")
               .SetValidator(new ValueEntryValidator());
       }
   }

   public class EventLogMapValidator : AbstractValidator<EventLogMap>
   {
       public EventLogMapValidator()
       {
           RuleFor(target => target.EventId).NotEmpty();
           RuleFor(target => target.Author).NotEmpty();
           RuleFor(target => target.Description).NotEmpty();

           RuleFor(target => target.Maps.Count).GreaterThan(0).When(t => t.Maps != null);

           RuleForEach(target => target.Maps).NotNull()
               .WithMessage(
                   "Values")
               .SetValidator(new MapEntryValidator());
       }
   }
}
