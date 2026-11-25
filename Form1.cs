using filetto_con_ai;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp13
{
    public partial class Form1 : Form
    {
        StatoDelGioco gioco = new StatoDelGioco();
        Button[,] bottoniDiGioco = new Button[3, 3];
        public Form1()
        {
            InitializeComponent();
            bottoniDiGioco[0, 0] = button1;
            bottoniDiGioco[0, 1] = button2;
            bottoniDiGioco[0, 2] = button3;
            bottoniDiGioco[1, 0] = button4;
            bottoniDiGioco[1, 1] = button5;
            bottoniDiGioco[1, 2] = button6;
            bottoniDiGioco[2, 0] = button7;
            bottoniDiGioco[2, 1] = button8;
            bottoniDiGioco[2, 2] = button9;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            gioco.caselle[0, 0].setContenuto('X');
            refreshCaselleView();

        }

        private void button2_Click(object sender, EventArgs e)
        {

            gioco.caselle[0, 1].setContenuto('X');
            refreshCaselleView();
        }

        private void button3_Click(object sender, EventArgs e)
        {

            gioco.caselle[0, 2].setContenuto('X');
            refreshCaselleView();
        }

        private void button4_Click(object sender, EventArgs e)
        {

            gioco.caselle[1, 0].setContenuto('X');
            refreshCaselleView();
        }

        private void button5_Click(object sender, EventArgs e)
        {

            gioco.caselle[1, 1].setContenuto('X');
            refreshCaselleView();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            gioco.caselle[1, 2].setContenuto('X');
            refreshCaselleView();

        }

        private void button7_Click(object sender, EventArgs e)
        {
            gioco.caselle[2, 0].setContenuto('X');
            refreshCaselleView();

        }

        private void button8_Click(object sender, EventArgs e)
        {

            gioco.caselle[2, 1].setContenuto('X');
            refreshCaselleView();
        }

        private void button9_Click(object sender, EventArgs e)
        {

            gioco.caselle[2, 2].setContenuto('X');
            refreshCaselleView();
        }

        private void refreshCaselleView()
        {
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    bottoniDiGioco[i, j].Text = gioco.caselle[i, j].getContenuto().ToString();
        }

        private async void button10_Click(object sender, EventArgs e)
        {
            button10.Enabled = false;
            try
            {
                await giocataAIAsync();
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Errore mossa AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                button10.Enabled = true;
            }
        }


        AiClient ai = new AiClient();
        private async Task giocataAIAsync()
        {
            String statoSerializzato = gioco.serializeStato();
            String domanda = "Sei un esperto di tris e mi devi battere con la prossima giocata, la situazione del tabellone in cui le mie mosse sono le 'X', le tue sono le 'O' e gli spazi liberi sono il carattere nullo, è questo: \n\r" + statoSerializzato + "\n\r, il tabellone è un 3x3, vince chi mette 3 segni in fila, orizzontale, verticale e obliqua, la rappresentazione del tabellone è riga per riga. Rispondimi facendo la tua migliore mossa che massimizza le tue probabilità di vincita senza aggiungere commenti o spiegazioni ma solamente scrivendomi la nuova griglia di gioco con la tua mossa fatta su uno spazio vuoto. Usa lo stesso schema JSON del tabellone, senza Markdown.";

            string risposta = await ai.AskAsync(domanda);

            string griglia = Regex.Replace(
                risposta.Trim(), @"\A```(?:json)?\s*(.*?)\s*```\z",
                "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            gioco.deserializzaStato(griglia);
            refreshCaselleView();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
