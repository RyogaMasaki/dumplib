﻿using System;
using System.IO;

namespace dumplib.Image
{
    public class SNKNeoGeoPocket_ROM : MediaImage
    {

        private readonly static string HW_Worldwide = "SNK NeoGeo Pocket / NeoGeo Pocket Color";
        private readonly static string HW_Japan = "SNK ネオジオポケット ／ ネオジオポケットカラー";

        public string HardwareName_Japan
        {
            get
            {
                return SNKNeoGeoPocket_ROM.HW_Japan;
            }
        }

        public string HardwareName_JapanRomaji
        {
            get
            {
                return SNKNeoGeoPocket_ROM.HW_Worldwide;
            }
        }

        public string HardwareName_Worldwide
        {
            get
            {
                return SNKNeoGeoPocket_ROM.HW_Worldwide;
            }
        }

        public SNKNeoGeoPocket_ROM(Stream Datastream, IDumpConverter Converter = null)
            : base(Datastream, Converter)
        {
            base.MediaType = MediaTypes.ROM;
            base.HardwareName = SNKNeoGeoPocket_ROM.HW_Worldwide;
            
            base.SoftwareTitle = dumplib.Text.Transcode.UsingASCII(GetBytes(0x24, 12));
        }
    }
}
