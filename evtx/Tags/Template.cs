using System;
using System.Collections.Generic;
using System.IO;

namespace evtx.Tags
{
    public class Template
    {
        public Template(int templateId, int templateOffset, Guid guid, BinaryReader payload, int nextTemplateOffset,
            long templateAbsoluteOffset, ChunkInfo chunk)
        {
            TemplateId = templateId;
            TemplateOffset = templateOffset;

            Size = payload.BaseStream.Length;
            TemplateGuid = guid;
            NextTemplateOffset = nextTemplateOffset;
            TemplateAbsoluteOffset = templateAbsoluteOffset;
            payload.BaseStream.Seek(0, SeekOrigin.Begin);

            Nodes = new List<IBinXml>();

            while (true)
            {
                var t = TagBuilder.BuildTag(templateOffset, payload, chunk);

                Nodes.Add(t);

                if (t is EndOfBXmlStream)
                {
                    break;
                }
            }
        }

        public List<IBinXml> Nodes { get; }

        /// <summary>
        ///     The size of the template itself. The total size from op code 0xC to the end of the template is Size + 0x22
        /// </summary>
        public long Size { get; }

        public int TemplateOffset { get; }
        public int NextTemplateOffset { get; }
        public int TemplateId { get; }
        public long TemplateAbsoluteOffset { get; }
        public Guid TemplateGuid { get; }

        public string AsXml()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return
                $"Absolute offset: 0x{TemplateAbsoluteOffset:X8} Template AbsoluteOffset 0x{TemplateOffset:X8} Next Template AbsoluteOffset 0x{NextTemplateOffset:X8}  Guid: {TemplateGuid} Size: 0x{Size:X4}";
        }
    }
}