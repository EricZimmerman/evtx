using System;
using System.Collections.Generic;
using FluentValidation;

namespace evtx
{
    public class EventLogMap
    {
        public string Author { get; set; }
        public int EventId { get; set; }
        public string Channel { get; set; }
        /// <summary>
        /// Optional field that, when present, will limit the processing of the map to those matching Provider and EventId
        /// </summary>
        public string Provider { get; set; }
        public string Description { get; set; }

        public List<MapEntry> Maps { get; set; }
        public List<LookupEntry> Lookups { get; set; }

        public override string ToString()
        {
            return
                $"EventId: {EventId} Channel: {Channel} Author: {Author} Description: {Description}, Map count: {Maps.Count:N0}";
        }
    }

    public class LookupEntry
    {
        public string Name { get; set; }
        public string Default { get; set; }

        public Dictionary<string,string> Values { get; set; }
    }

    public class MapEntry
    {
        public string Property { get; set; }
        public string PropertyValue { get; set; }

        public List<ValueEntry> Values { get; set; }

        public override string ToString()
        {
            return
                $"Property: {Property} PropertyValue: {PropertyValue}, Values count: {Values.Count:N0}";
        }
    }

    public class ValueEntry
    {
        public string Name { get; set; }
        public string Value { get; set; }
        /// <summary>
        /// A regex pattern to run against Value. Optional
        /// </summary>
        public string Refine { get; set; }

        public override string ToString()
        {
            return $"Name: {Name} Value: {Value} Refine: {Refine}";
        }
    }

    public class LookupEntryValidator : AbstractValidator<LookupEntry>
    {
        public LookupEntryValidator()
        {
            RuleFor(target => target.Name).NotEmpty();
            RuleFor(target => target.Default).NotEmpty();
            

            RuleFor(target => target.Values.Count).GreaterThan(0).When(t => t.Values != null);

            RuleForEach(t => t.Values.Keys).NotNull().WithMessage("Keys");
            RuleForEach(t => t.Values.Values).NotNull().WithMessage("Values");
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

            RuleFor(target => target.Property)
                .Matches(
                    "^UserName$|^RemoteHost$|^ExecutableInfo$|^PayloadData1$|^PayloadData2$|^PayloadData3$|^PayloadData4$|^PayloadData5$|^PayloadData6$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase).WithMessage("\r\n'Property' must be one of the following: UserName|RemoteHost|ExecutableInfo|PayloadData1|PayloadData2|PayloadData3|PayloadData4|PayloadData5|PayloadData6");

            RuleFor(target => target.PropertyValue).NotEmpty();

            RuleFor(target => target.Values.Count).GreaterThan(0).When(t => t.Values != null);

            RuleForEach(target => target.Values).NotNull()
                .WithMessage(
                    "Values")
                .NotEmpty();
        }

    }

   

    public class EventLogMapValidator : AbstractValidator<EventLogMap>
    {
        public EventLogMapValidator()
        {
            RuleFor(target => target.EventId).NotNull();
            RuleFor(target => target.Channel).NotEmpty();
            RuleFor(target => target.Provider).NotEmpty();
            
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