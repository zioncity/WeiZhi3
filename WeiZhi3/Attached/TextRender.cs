﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Weibo.ViewModels;
using Weibo.ViewModels.StatusRender;

namespace WeiZhi3.Attached
{
    /*
             Reserved = 0,
        Part,
        Punctuation,//ends with ,.?!and some punctuations
        Quote,//"quote" 《，
        Hyperlink , //http://t.cn/xxx
        Name , // @name
        CopyedFrom , // //@xxx:
        Topic , // #xxx#
        Emotion, // [xxx],【，L
        End ,//last sentence ,with or without punctuation
        ReplyTo ,//回复Name:        
*/
    internal class TextRenderContext
    {
        internal bool HasCopyedFromItem;
    }
    class TextRender
    {
        static void InsertToken(ICollection<Inline> ic, Token t, TextRenderContext ctx)
        {
            switch (t.tag)
            {
            case TokenTypes.Hyperlink:
                InsertHyperLink(ic, t);
                break;
            case TokenTypes.Name:
                InsertMentionName(ic, t);
                break;
            case TokenTypes.Emotion:
                InsertEmotion(ic, t);
                break;
            case TokenTypes.CopyedFrom:
                InsertCopyedFrom(ic, t,ctx);
                break;
            case TokenTypes.ReplyTo:
                InsertReplyto(ic, t);
                break;
            case TokenTypes.Topic:
                {
                    InsertTopic(ic, t);
                }
                break;
            case TokenTypes.Quote:
                {
                    InsertQuote(ic, t);
                }
                break;
            case TokenTypes.Writer:
                    {
                        InsertWriter(ic, t);
                        break;
                    }
            case TokenTypes.Break:
                {
                    InsertBreak(ic);
                    break;
                }
            //case TokenTypes.End:
            //case TokenTypes.Punctuation:
            //case TokenTypes.Part:
            //case TokenTypes.Reserved:
            default:
                InsertNormal(ic, t);
                break;
            }
        }

        private static void InsertTokens(TextBlock textblock, WeiboStatus status, TextRenderContext ctx)
        {
            //DesignerProperties.IsInDesignMode
            if (textblock == null)
                return;
            if ((bool)textblock.GetValue(DesignerProperties.IsInDesignModeProperty))
                return;
            if (status == null)
                return;
            textblock.Inlines.Clear();
            var ic = new List<Inline>();
            foreach (var sent in status.tokens)
            {
                InsertToken(ic, sent,ctx);
            }
            textblock.Inlines.AddRange(ic);
            if (textblock.Inlines.Count == 0)
                textblock.Visibility = Visibility.Collapsed;
        }
        private static void InsertNormal(ICollection<Inline> textblock, Token t)
        {
            if (string.IsNullOrEmpty(t.text))
                return;
            textblock.Add(new Run(t.text));
        }
        private static void InsertBreak(ICollection<Inline> tb)
        {
            tb.Add(new LineBreak());
        }
        private static void InsertTopic(ICollection<Inline> textblock, Token t)
        {
            var topic = t.text;
            if (string.IsNullOrEmpty(topic))
                return;
            textblock.Add(new Run("#"));
            textblock.Add(new Run(topic));
            textblock.Add(new Run("#"));
        }
        private static void InsertReplyto(ICollection<Inline> textblock, Token t)
        {
            var name = t.text;
            if (string.IsNullOrEmpty(name))
                return;
            textblock.Add(new Run("回复"));
            var h = new Hyperlink(new Run(name));

            //h.SetResourceReference(Control.ForegroundProperty, "MetroColorText");
            textblock.Add(h);
            textblock.Add(new Run("："));
        }
        private static void InsertCopyedFrom(ICollection<Inline> textblock, Token t, TextRenderContext ctx)
        {
            var name = t.text;
            if (string.IsNullOrEmpty(name))
                return;
            if(!ctx.HasCopyedFromItem)
            {
                ctx.HasCopyedFromItem = true;
                textblock.Add(new LineBreak());
            }
            textblock.Add(new Run("//@"));
            var h = new Hyperlink(new Run(name));
            //h.SetResourceReference(Control.ForegroundProperty, "MetroColorText");
            textblock.Add(h);

            textblock.Add(new Run("："));
        }
        private static void InsertQuote(ICollection<Inline> textblock, Token t)
        {
            var q = t.text.Trim();
            if (string.IsNullOrEmpty(q))
                return;
            textblock.Add(new Run(string.Format("“{0}”", q)));
        }
        private static void InsertEmotion(ICollection<Inline> textblock, Token t)
        {
            var emo = t.text.Trim();
            if (string.IsNullOrEmpty(emo))
                return;
            textblock.Add(new Run(string.Format("[{0}]", emo)));
        }
        private static void InsertWriter(ICollection<Inline> textblock, Token t)
        {
            var name = t.text;
            if (string.IsNullOrEmpty(name))
                return;
            textblock.Add(new Run(t.text + "："));
        }
        private static void InsertMentionName(ICollection<Inline> textblock, Token t)
        {
            var name = t.text;
            if (string.IsNullOrEmpty(name))
                return;
            textblock.Add(new Run("@"));

            var h = new Hyperlink(new Run(name));
            //h.SetResourceReference(Control.ForegroundProperty, "MetroColorText");
            textblock.Add(h);
        }
        //private static bool InsertName(ICollection<Inline> textblock, string name)
        //{
        //    if (string.IsNullOrEmpty(name))
        //        return false;
        //    textblock.Add(new Hyperlink(new Run(name)));
        //    textblock.Add(new Run("："));
        //    return true;
        //}
        private static void InsertHyperLink(ICollection<Inline> textblock, Token token)
        {
            var ut = token.text.Trim();
            if (string.IsNullOrEmpty(ut))
                return;

            if (ut.Length != token.text.Length)
                textblock.Add(new Run(" "));

            //create hyperlink 
            var h = new Hyperlink(new Run(ut.Replace("http://t.cn/", string.Empty)))
            {
                //Command = WeiZhiCommands.NavigateUrlCommand,
                CommandParameter = ut,
            };

            textblock.Add(h);

            if (ut.Length != token.text.Length)
                textblock.Add(new Run(" "));
        }

        #region Property Weibo 
        public static readonly DependencyProperty WeiboProperty
            = DependencyProperty.RegisterAttached("Weibo",
                typeof(WeiboStatus), typeof(TextRender)
                , new FrameworkPropertyMetadata(OnWeiboPropertyChanged));

        public static void SetWeibo(TextBlock textblock, WeiboStatus status)
        {
            textblock.SetValue(WeiboProperty, status);
        }
        public static WeiboStatus GetWeibo(TextBlock textblock)
        {
            return textblock.GetValue(WeiboProperty) as WeiboStatus;
        }
        private static void OnWeiboPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var textblock = (TextBlock)d;
            var status = (WeiboStatus)e.NewValue;
            var ctx = new TextRenderContext();

            InsertTokens(textblock, status, ctx);
        }
        #endregion
    }

}