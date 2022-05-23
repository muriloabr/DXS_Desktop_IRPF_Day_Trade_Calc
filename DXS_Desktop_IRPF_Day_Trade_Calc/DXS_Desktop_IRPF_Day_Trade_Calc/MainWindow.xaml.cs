using System;
using System.Windows;
using Microsoft.Win32;
using System.Runtime;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text.Json;
using System.Threading.Tasks;

namespace DXS_Desktop_IRPF_Day_Trade_Calc
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void btnLimpar_Click(object sender, RoutedEventArgs e)
        {
            this.txtEditor.Text = "";
        }
        private async void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            List<Task> tasks = new List<Task>();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "Text files (*.pdf)|*.pdf|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                //Todas as páginas processadas!
                List<TituloValor> listaPaginas = new List<TituloValor>();
                int arquivo = 1;

                Task t = new Task(() =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        lblStatus.Content = Tarefa.Processando;
                    });
                    //Para cada arquivo selecionado!
                    foreach (string filename in openFileDialog.FileNames)
                    {
                        this.Dispatcher.Invoke(() =>
                        {   
                            lblArquivos.Content = ($"{arquivo}/{openFileDialog.FileNames.Length}");
                        });

                        var src = filename;
                        PdfDocument documento = LerPdf(src);


                        //Para cada página do arquivo que está sendo processado!
                        for (int paginaAtual = 1; paginaAtual <= documento.GetNumberOfPages(); paginaAtual++)
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                lblPaginas.Content = ($"{paginaAtual}/{ documento.GetNumberOfPages()}");
                            });

                            string pagina = lerPaginaPdf(documento, paginaAtual);
                            string[] linhasPagina = pagina.Split("\n");

                            this.Dispatcher.Invoke(() =>
                            {
                                if ((chkIncidencia.IsChecked == true) || (chkValorNegociado.IsChecked == true))
                                {
                                    //Adiciona a página processada na lista de páginas!
                                    listaPaginas.Add(EstruturarPaginaPdf_TitulosDeInteresse(linhasPagina));
                                }
                                else
                                {
                                    listaPaginas.Add(EstruturarPaginaPdf_Todos(linhasPagina));
                                }
                            });

                        }
                        arquivo++;
                    }
                    this.Dispatcher.Invoke(() =>
                    {
                        //Exibindo a lista de páginas processadas!
                        if (chkIncidencia.IsChecked == true)
                        {
                            txtEditor.Text += JsonSerializer.Serialize(SintetizarPaginasPorCalendariosNegociacoes(ComputarDayTradeIncidencia(listaPaginas)),
                                     new JsonSerializerOptions
                                     {
                                         WriteIndented = true
                                     });
                        }
                        else if (chkValorNegociado.IsChecked == true)
                        {
                            txtEditor.Text += JsonSerializer.Serialize(SintetizarPaginasPorCalendariosNegociacoes(ComputarDayTrade(listaPaginas)),
                                 new JsonSerializerOptions
                                 {
                                     WriteIndented = true
                                 });
                        }
                        else if (chkDetalhes.IsChecked == true)
                        {
                            txtEditor.Text += JsonSerializer.Serialize(SintetizarPaginasPorCalendariosDetalhesNota(listaPaginas),
                                 new JsonSerializerOptions
                                 {
                                     WriteIndented = true
                                 });
                        }
                    });
                });
                lblStatus.Content = (Tarefa)t.Status;
                tasks.Add(t);
                lblStatus.Content = (Tarefa)t.Status;
                t.Start();                
                await Task.WhenAll(tasks.ToArray());
                lblStatus.Content = (Tarefa)t.Status;
            }
        }
        public enum Tarefa
        {
            //
            // Summary:
            //     The task has been initialized but has not yet been scheduled.
            Criada,
            //
            // Summary:
            //     The task is waiting to be activated and scheduled internally by the .NET infrastructure.
            Agendada,
            //
            // Summary:
            //     The task has been scheduled for execution but has not yet begun executing.
            Aguardando,
            //
            // Summary:
            //     The task is running but has not yet completed.
            Processando,
            //
            // Summary:
            //     The task has finished executing and is implicitly waiting for attached child
            //     tasks to complete.
            Procedendo,
            //
            // Summary:
            //     The task completed execution successfully.
            Completa,
            //
            // Summary:
            //     The task acknowledged cancellation by throwing an OperationCanceledException
            //     with its own CancellationToken while the token was in signaled state, or the
            //     task's CancellationToken was already signaled before the task started executing.
            //     For more information, see Task Cancellation.
            Cancelada,
            //
            // Summary:
            //     The task completed due to an unhandled exception.
            Falhou
        }
        private TituloValor EstruturarPaginaPdf_TitulosDeInteresse(string[] textos)
        {
            //Configuração padrão
            TituloValor itemNovo = new TituloValor();
            bool inicioLeitura = false;
            bool isTitulo = true;
            bool isPrimeiroEspaco = true;

            //Configuração restrita ao PDF!
            string[] titulosDeInteresse = new string[]
            {
                "C/V Mercadoria Vencimento Quantidade Preço/ajuste Tipo do Negócio Vlr. de Operação/Ajuste D/C Taxa operacional"
            };
            string tituloDeLeitura = "Transações";


            foreach (var texto in textos)
            {
                //Não é inicio de leitura ainda!
                if (!inicioLeitura)
                {
                    //Comparo este texto com algum titulo de interesse!
                    foreach (string titulo in titulosDeInteresse)
                    {
                        //É um titulo de interesse!
                        if (codificarString(titulo).Equals(codificarString(texto)))
                        {
                            if (isTitulo)
                            {
                                itemNovo.Titulo = codificarString(tituloDeLeitura.TrimEnd().TrimStart());
                                isTitulo = false;
                            }
                            //Aviso que comecamos a leitura de interesse e vamos para o próximo texto!
                            inicioLeitura = true;
                            break;
                        }
                        //Não é um titulo de intersse!
                        else
                        {
                            //Pode ser o ano do calendario da nota!
                            try
                            {
                                int ano, mes, dia;
                                string[] possivelData = texto.TrimEnd().TrimStart().Split(" ");
                                string[] data;
                                foreach (string item in possivelData)
                                {
                                    if (item.Contains('/'))
                                    {
                                        data = item.TrimEnd().TrimStart().Split('/');
                                        int.TryParse(data[0], out dia);
                                        int.TryParse(data[1], out mes);
                                        int.TryParse(data[2], out ano);
                                        DateTime calendario = new DateTime(ano, mes, dia);
                                        itemNovo.Titulo = codificarString(calendario.Day + "/" + calendario.Month + "/" + calendario.Year);
                                        isTitulo = false;
                                        break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                //Não faz nada
                            }
                        }
                    }
                    //vamos para o próximo sem encontrar o título de interesse!
                    continue;
                }

                //É inicio de leitura!

                //É a vez de montar um título!
                if (isTitulo)
                {
                    //Registro o titulo de leitura no objeto da lista!
                    itemNovo.Titulo = codificarString(tituloDeLeitura.TrimEnd().TrimStart());
                    //Aviso que já temos um título!
                    isTitulo = false;
                }
                //É a vez de montar o valor!
                else
                {
                    //Tem título + primeiro espaçamento depois do título e não tem valor
                    if ((!string.IsNullOrEmpty(itemNovo.Titulo.Trim()))
                        && (string.IsNullOrEmpty(texto.Trim())) && (isPrimeiroEspaco))
                    {
                        //Aviso que o primeiro espaço já passou e vamos para o próximo texto!
                        isPrimeiroEspaco = false;
                        continue;
                    }
                    else
                    {
                        //Texto é vazio!
                        if (string.IsNullOrEmpty(texto.Trim()))
                        {
                            return itemNovo;
                        }
                        else
                        {
                            //Adicionar valor à lista!
                            itemNovo.Valor.Add(separarElementosEspacados(codificarString(texto)));
                        }
                    }
                }
            }
            return itemNovo;
        }
        private TituloValor EstruturarPaginaPdf_Todos(string[] textos)
        {
            //Configuração padrão
            TituloValor itemNovo = new TituloValor();
            bool inicioLeitura = false;
            bool isValorEncontrado = false;

            //Configuração restrita ao PDF!
            List<TituloEncontrado> titulosDeInteresse = new List<TituloEncontrado>();
            titulosDeInteresse.Add(new TituloEncontrado("Total das despesas", false));
            titulosDeInteresse.Add(new TituloEncontrado("Total líquido da nota", false));
            string tituloDoValor = "";


            foreach (var texto in textos)
            {
                //Não é inicio de leitura ainda!
                if (!inicioLeitura)
                {
                    //Pode ser o ano do calendario da nota!
                    try
                    {
                        int ano, mes, dia;
                        string[] possivelData = texto.TrimEnd().TrimStart().Split(" ");
                        string[] data;
                        foreach (string item in possivelData)
                        {
                            if (item.Contains('/'))
                            {
                                data = item.TrimEnd().TrimStart().Split('/');
                                int.TryParse(data[0], out dia);
                                int.TryParse(data[1], out mes);
                                int.TryParse(data[2], out ano);
                                DateTime calendario = new DateTime(ano, mes, dia);
                                itemNovo.Titulo = codificarString(calendario.Day + "/" + calendario.Month + "/" + calendario.Year);
                                inicioLeitura = true;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Não faz nada
                    }
                }
                else
                {
                    if (isValorEncontrado)
                    {
                        List<string> listaRapida = new List<string>() {
                                tituloDoValor, (codificarString(texto)).Split("|")[0].Trim()
                            };

                        itemNovo.Valor.Add(listaRapida);
                        tituloDoValor = "";
                        isValorEncontrado = false;
                    }
                    else
                    {
                        foreach (TituloEncontrado titulo in titulosDeInteresse)
                        {
                            if (codificarString(titulo.Titulo).Equals(codificarString(texto)))
                            {
                                tituloDoValor = codificarString(texto);
                                isValorEncontrado = true;
                                titulo.Encontrado = true;
                            }
                        }
                    }
                }
                bool isEnd = true;
                foreach (TituloEncontrado titulo in titulosDeInteresse)
                {
                    if (!titulo.Encontrado)
                    {
                        isEnd = false;
                    }
                }
                if (isEnd)
                {
                    break;
                }
            }
            return itemNovo;
        }
        private PdfDocument LerPdf(string path)
        {
            PdfReader pdfLeitor = new PdfReader(path);
            PdfDocument pdfDoc = new PdfDocument(pdfLeitor);
            return pdfDoc;
        }
        private string lerPaginaPdf(PdfDocument pdfDoc, int pagina)
        {
            ITextExtractionStrategy stry = new SimpleTextExtractionStrategy();
            return PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(pagina), stry);
        }
        private List<string> separarElementosEspacados(string elementos)
        {
            return new List<string>(elementos.Split(" "));
        }
        private string codificarString(string texto)
        {
            byte[] bytes = Encoding.Latin1.GetBytes(texto);
            return Encoding.Latin1.GetString(bytes);
        }
        private class TituloValor
        {
            private string titulo;
            private List<List<string>> valor;

            public string Titulo { get { return this.titulo; } set { this.titulo = value; } }
            public List<List<string>> Valor { get { return this.valor; } set { this.valor = value; } }

            public TituloValor()
            {
                this.titulo = "";
                this.valor = new List<List<string>>();
            }
            public TituloValor(string titulo, string[] valor)
            {
                this.titulo = titulo;
                this.valor = new List<List<string>>();
                this.valor.Add(new List<string>(valor));
            }
        }
        private class TituloEncontrado
        {
            private string titulo;
            private bool encontrado;

            public string Titulo { get { return this.titulo; } set { this.titulo = value; } }
            public bool Encontrado { get { return this.encontrado; } set { this.encontrado = value; } }

            public TituloEncontrado()
            {
                this.titulo = "";
                this.encontrado = false;
            }
            public TituloEncontrado(string titulo, bool encontrado)
            {
                this.titulo = titulo;
                this.encontrado = encontrado;
            }

        }
        private string QualPolarizacao(Dictionary<string, string> dado, string tipo)
        {
            string valorAnteriorPuro = "";
            double valorAnterior = double.MinValue;
            dado.TryGetValue(tipo, out valorAnteriorPuro);
            double.TryParse(valorAnteriorPuro, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out valorAnterior);

            switch (valorAnterior)
            {
                case >= 0:
                    return "C";
                case < 0:
                    return "D";
                default:
                    throw new Exception("Polarização não soube identificar!");
            }
        }
        private double PolarizarValor(double valor, string polar)
        {
            switch (polar)
            {
                case "C":
                    return (valor >= 0) ? valor : valor * (-1);
                case "D":
                    return (valor < 0) ? valor : valor * (-1);
                default:
                    throw new Exception("Polarização de valor deu errado!");
            }
        }
        private List<Dictionary<string, string>> SintetizarPaginasPorCalendariosNegociacoes(List<Dictionary<string, string>> listaComputada)
        {
            List<Dictionary<string, string>> listaSintetizada = new List<Dictionary<string, string>>();
            string calendario = "";
            Dictionary<string, string> calculoSintetizado = new Dictionary<string, string>();

            //Para cada item da lista computada
            foreach (Dictionary<string, string> calculo in listaComputada)
            {
                //Ja tem calendario e Data diferente, então adiciono a lista sintetizada!
                if (!string.IsNullOrEmpty(calendario))
                {
                    if (!calendario.Equals(calculo["DATA"]))
                    {
                        listaSintetizada.Add(calculoSintetizado);
                        calendario = "";
                        calculoSintetizado = new Dictionary<string, string>();
                    }
                }
                //Não tem calendario atual
                if (string.IsNullOrEmpty(calendario))
                {
                    //Seto todo dados do primeiro item
                    calendario = calculo["DATA"];
                    calculoSintetizado["DATA"] = calculo["DATA"];

                    if (calculo.ContainsKey("WIN"))
                    {
                        calculoSintetizado["WIN"] = calculo["WIN"];
                    }

                    if (calculo.ContainsKey("WDO"))
                    {
                        calculoSintetizado["WDO"] = calculo["WDO"];
                    }
                }
                //Tem calendario atual
                else
                {
                    //Sintetizo com o anterior!
                    List<string> valoreComoNoPdf = new List<string>() { "", "", "", "", "", "", "", "", "", "" };
                    if (calculo.ContainsKey("WIN"))
                    {
                        valoreComoNoPdf[8] = calculo["WIN"];
                        valoreComoNoPdf[9] = QualPolarizacao(calculo, "WIN");
                        SomarValorDoPDfAoDado(ref calculoSintetizado, valoreComoNoPdf, "WIN");
                    }

                    if (calculo.ContainsKey("WDO"))
                    {
                        valoreComoNoPdf[8] = calculo["WDO"];
                        valoreComoNoPdf[9] = QualPolarizacao(calculo, "WDO");
                        SomarValorDoPDfAoDado(ref calculoSintetizado, valoreComoNoPdf, "WDO");
                    }

                    //Quando é o ultimo adiciono na lista sintetizada!
                    if (calculo.Equals(listaComputada[listaComputada.Count - 1]))
                    {
                        listaSintetizada.Add(calculoSintetizado);
                        calculoSintetizado = new Dictionary<string, string>();
                    }
                }
            }
            return listaSintetizada;
        }
        private List<Dictionary<string, string>> SintetizarPaginasPorCalendariosDetalhesNota(List<TituloValor> lista)
        {
            List<Dictionary<string, string>> listaSintetizada = new List<Dictionary<string, string>>();
            string calendario = "";
            Dictionary<string, string> calculoSintetizado = new Dictionary<string, string>();

            //Para cada item da lista computada
            foreach (TituloValor calculo in lista)
            {
                //Ja tem calendario e Data diferente, então adiciono a lista sintetizada!
                if (!string.IsNullOrEmpty(calendario))
                {
                    if (!calendario.Equals(calculo.Titulo))
                    {
                        //Seto todo dados do primeiro item
                        calendario = calculo.Titulo;
                        calculoSintetizado["DATA"] = calculo.Titulo;
                        foreach (List<string> valor in calculo.Valor)
                        {
                            calculoSintetizado[valor[0]] = valor[1];
                        }
                        listaSintetizada.Add(calculoSintetizado);
                        calculoSintetizado = new Dictionary<string, string>();
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    //Seto todo dados do primeiro item
                    calendario = calculo.Titulo;
                    calculoSintetizado["DATA"] = calculo.Titulo;
                    foreach (List<string> valor in calculo.Valor)
                    {
                        calculoSintetizado[valor[0]] = valor[1];
                    }
                    listaSintetizada.Add(calculoSintetizado);
                    calculoSintetizado = new Dictionary<string, string>();
                }
            }
            return listaSintetizada;
        }
        private bool SomarValorDoPDfAoDado(ref Dictionary<string, string> dado, List<string> valoresNovos, string tipo)
        {
            if (dado.ContainsKey(tipo))
            {
                //Pega o valor que estava e soma com o novo;
                string valorAnteriorPuro = "";
                double valorAnterior = double.MinValue;
                dado.TryGetValue(tipo, out valorAnteriorPuro);
                double.TryParse(valorAnteriorPuro, NumberStyles.Currency, CultureInfo.GetCultureInfo("pt-BR"), out valorAnterior);

                if (valorAnterior != double.MinValue)
                {
                    double valor = double.MinValue;

                    if (valoresNovos.Count < 9)
                        return false;

                    double.TryParse(valoresNovos[8], NumberStyles.Currency, CultureInfo.GetCultureInfo("pt-BR"), out valor);
                    valorAnterior = Math.Round(valorAnterior, 2);
                    valor = (valor != double.MinValue) ? valorAnterior + PolarizarValor(valor, valoresNovos[9]) : throw new Exception("Falha ao acessar valor 3!");
                    valor = Math.Round(valor, 2);
                    dado[tipo] = valor.ToString();
                }
                else
                {
                    throw new Exception("Falha ao acessar valor ao calcular!");
                }
            }
            return true;
        }
        private List<Dictionary<string, string>> ComputarDayTrade(List<TituloValor> listaPaginas)
        {
            //Configuracao do PDF
            List<Dictionary<string, string>> listaComputada = new List<Dictionary<string, string>>();

            //Para cada página atual da lista de paginas processadas!
            foreach (TituloValor paginaAtual in listaPaginas)
            {
                Dictionary<string, string> dadoComputadoPaginaAtual = new Dictionary<string, string>();
                dadoComputadoPaginaAtual["DATA"] = paginaAtual.Titulo;

                //Para cada lista de valores dentro de valor
                foreach (List<string> valores in paginaAtual.Valor)
                {
                    //WIN
                    if (valores[1].Trim().Equals("WIN"))
                    {
                        if (dadoComputadoPaginaAtual.ContainsKey("WIN"))
                        {
                            SomarValorDoPDfAoDado(ref dadoComputadoPaginaAtual, valores, "WIN");
                        }
                        else
                        {
                            double valor = double.MinValue;

                            if (valores.Count < 9)
                                continue;

                            double.TryParse(valores[8], NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out valor);
                            dadoComputadoPaginaAtual.Add("WIN",
                                (valor != double.MinValue) ? PolarizarValor(valor, valores[9]).ToString() : throw new Exception("Falha ao acessar valor 2!"));
                        }
                    }
                    //WDO
                    if (valores[1].Trim().Equals("WDO"))
                    {
                        if (dadoComputadoPaginaAtual.ContainsKey("WDO"))
                        {
                            SomarValorDoPDfAoDado(ref dadoComputadoPaginaAtual, valores, "WDO");
                        }
                        else
                        {
                            double valor = double.MinValue;

                            if (valores.Count < 9)
                                continue;

                            double.TryParse(valores[8], NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out valor);
                            dadoComputadoPaginaAtual.Add("WDO",
                                (valor != double.MinValue) ? PolarizarValor(valor, valores[9]).ToString() : throw new Exception("Falha ao acessar valor 2!"));
                        }
                    }
                }
                listaComputada.Add(dadoComputadoPaginaAtual);
            }
            return listaComputada;
        }
        private List<Dictionary<string, string>> ComputarDayTradeIncidencia(List<TituloValor> listaPaginas)
        {
            //Configuracao do PDF
            List<Dictionary<string, string>> listaComputada = new List<Dictionary<string, string>>();
            List<string> incidencia = new List<string>() { "", "", "", "", "", "", "", "", "", "" };
            incidencia[8] = "1";
            incidencia[9] = "C";
            //Para cada página atual da lista de paginas processadas!
            foreach (TituloValor paginaAtual in listaPaginas)
            {
                Dictionary<string, string> dadoComputadoPaginaAtual = new Dictionary<string, string>();
                dadoComputadoPaginaAtual["DATA"] = paginaAtual.Titulo;

                //Para cada lista de valores dentro de valor
                foreach (List<string> valores in paginaAtual.Valor)
                {
                    //WIN
                    if (valores[1].Trim().Equals("WIN"))
                    {
                        if (dadoComputadoPaginaAtual.ContainsKey("WIN"))
                        {
                            SomarValorDoPDfAoDado(ref dadoComputadoPaginaAtual, incidencia, "WIN");
                        }
                        else
                        {
                            double valor = double.MinValue;

                            if (valores.Count < 9)
                                continue;

                            double.TryParse(incidencia[8], NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out valor);
                            dadoComputadoPaginaAtual.Add("WIN",
                                (valor != double.MinValue) ? PolarizarValor(valor, incidencia[9]).ToString() : throw new Exception("Falha ao acessar valor 2!"));
                        }
                    }
                    //WDO
                    if (valores[1].Trim().Equals("WDO"))
                    {
                        if (dadoComputadoPaginaAtual.ContainsKey("WDO"))
                        {
                            SomarValorDoPDfAoDado(ref dadoComputadoPaginaAtual, incidencia, "WDO");
                        }
                        else
                        {
                            double valor = double.MinValue;

                            if (valores.Count < 9)
                                continue;

                            double.TryParse(incidencia[8], NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out valor);
                            dadoComputadoPaginaAtual.Add("WDO",
                                (valor != double.MinValue) ? PolarizarValor(valor, incidencia[9]).ToString() : throw new Exception("Falha ao acessar valor 2!"));
                        }
                    }
                }
                listaComputada.Add(dadoComputadoPaginaAtual);
            }
            return listaComputada;
        }
    }
}
