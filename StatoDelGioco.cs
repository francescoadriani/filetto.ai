using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp13
{
    public class StatoDelGioco
    {
        public CasellaDiGioco[,] caselle = new CasellaDiGioco[3, 3];
        public StatoDelGioco()
        {
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    caselle[i, j] = new CasellaDiGioco('\0');
        }

        public String serializeStato()
        {/*
            String current = "";
            for (int i = 0; i < 3; i++)
            {
                current += "[";
                for (int j = 0; j < 3; j++)
                {
                    current = current + caselle[i, j].getContenuto();
                    if (j < 2)
                        current += ",";
                }
                current += "]";
                if (i<2)
                    current += ",";
            }
            
            return current;*/
            return JsonConvert.SerializeObject(caselle, Formatting.Indented);
        }

        public void deserializzaStato(String statoStringa)
        {
            this.caselle = JsonConvert.DeserializeObject< CasellaDiGioco[,] >(statoStringa);
        }
    }
}
