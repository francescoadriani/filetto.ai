using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp13
{
    public class CasellaDiGioco
    {
        public char giocata = '\0'; //può essere 'X', 'O' o niente '\0'
        public CasellaDiGioco(char giocata)
        {
            this.giocata = giocata;
        }

        public void setContenuto(char giocata)
        {
            this.giocata = giocata;
        }

        public char getContenuto()
        {
            return this.giocata;
        }

    }
}
