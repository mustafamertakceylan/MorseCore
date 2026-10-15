using MorseCore.AppCore.Radio;
using Microsoft.UI.Xaml.Media;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private string glossaryQuery="";
    private int glossaryCategory;
    private void BuildGlossary()
    {
        Heading(T("glossary"),T("glossaryHint"));
        var filters=new StackPanel { Spacing=12 };
        var search=new TextBox { Header=T("searchTerms"),Text=glossaryQuery,MaxLength=80 };
        string[] categories=["allTerms","qcodes","abbreviations","prosigns","phonetics"];
        var choice=Choice(T("category"),categories.Select(T),glossaryCategory,_=> { }); filters.Children.Add(Columns(260,2,search,choice));
        PageContent.Children.Add(Card(filters));
        var entries=new StackPanel { Spacing=8 }; PageContent.Children.Add(entries);
        void Refresh()
        {
            entries.Children.Clear(); var cards=new List<FrameworkElement>(); string query=glossaryQuery.Trim();
            foreach(var term in CwLearning.Terms.Where(t=>(glossaryCategory==0 || t.Category==categories[glossaryCategory]) && (t.Code.Contains(query,StringComparison.OrdinalIgnoreCase) || T("term_"+t.Key).Contains(query,StringComparison.CurrentCultureIgnoreCase))))
            {
                var body=new StackPanel { Spacing=6 };
                var header=Row(); var code=Label(term.Code,18); code.FontFamily=new FontFamily("Consolas"); code.FlowDirection=FlowDirection.LeftToRight;
                header.Children.Add(code); header.Children.Add(IconAction("play",()=>PlayMorse(CwMessages.Encode(term.Code),'/'),"\uE768"));
                body.Children.Add(header); body.Children.Add(Label(T("term_"+term.Key),14));
                var example=Label(term.Example,13,true); example.FlowDirection=FlowDirection.LeftToRight; body.Children.Add(example); cards.Add(Card(body));
            }
            if(glossaryCategory is 0 or 4)
                for(int i=0;i<CwLearning.Phonetics.Count;i++)
                {
                    string value=$"{(char)('A'+i)} · {CwLearning.Phonetics[i]}";
                    if(!value.Contains(query,StringComparison.OrdinalIgnoreCase)) continue;
                    var label=Label(value,15); label.FlowDirection=FlowDirection.LeftToRight; cards.Add(Card(label));
                }
            if(cards.Count==0) entries.Children.Add(Label(T("noResults"),14,true));
            else entries.Children.Add(Columns(320,2,cards.ToArray()));
        }
        search.TextChanged+=(_,_)=> { glossaryQuery=search.Text; Refresh(); };
        choice.SelectionChanged+=(_,_)=> { glossaryCategory=choice.SelectedIndex; Refresh(); }; Refresh();
    }
}
