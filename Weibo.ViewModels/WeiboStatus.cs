﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using Newtonsoft.Json.Linq;
using Weibo.Apis.SinaV2;
using Weibo.DataModel;
using Weibo.DataModel.Misc;
using Weibo.ViewModels.StatusRender;

namespace Weibo.ViewModels
{
    public interface IWeiboAccessToken
    {
        string get();
        long id();
    }
    public enum WeiboTopicSource
    {
        Reserved=0,
        FirstSentence,
        Quote,
        Trend ,
        Url,
        UrlContent,
        Retweeted,
    }
    public class WeiboEditor:ObservableObjectExt
    {
        
    }
    public class WeiboUrl : ObservableObjectExt
    {
        private UrlInfo _data = new UrlInfo();
        private string _music;
        public void assign(UrlInfo ui)
        {
            _data = ui;
        }
        bool has_annotations { get { return _data != null && _data.annotations != null && _data.annotations.Length > 0; } }
        public string topic 
        {
            get { return _data.topic(); } 
        }//from urlinfo or it's annotations

        public string short_path { get { return _data.url_short.Remove(0, 12); } }//not include http://t.cn/.Length == 12

        public bool has_document { get { return _data.type == UrlType.Normal || _data.type == UrlType.News || _data.type == UrlType.Blog; } }
        public bool has_music { get { return _data.type == UrlType.Music; } }
        public bool has_video { get { return _data.type == UrlType.Video; } }
        public bool has_media
        {
            get
            {
                return has_music || has_video;
            }
        }

        public UrlInfo data { get { return _data; } }//used for media play
        public UrlType type { get { return _data.type; } }

        public string pic { get { return has_annotations ? _data.annotations[0].pic : null; } }
        public string author { get { return has_annotations ? _data.annotations[0].author : null; } }
        public string url_long { get { return _data.url_long; } }
        public string video{ get { return has_annotations && has_video ? _data.annotations[0].url : null; } }//video url
        public string music { get { return _music; } set { Set(ref _music, value); } }//需要初始化过程中获取
        public string document { get { return has_document ? _data.url_short : null; } }//通过这个才能转化成flowdocument
        public string album { get { return has_annotations ? _data.annotations[0].album : null; } }
        public string mp4 { get { return has_annotations ? _data.annotations[0].mp4 : null; } }//used for sina video
    }
    public class WeiboStatus : ObservableObjectExt
    {
        //private bool _has_music;
        //private bool _has_document;
        //private string _mp4;
        //private string _document;
        private WeiboEditor _editor;
        private CommentsViewModel _comments;
        public DateTime created_at { get; set; }
        public string text { get; set; }
        public long id { get; set; }
        public long mid { get; set; }

        public string thumbnail_pic
        {
            get;
            set;
        }
        public string bmiddle_pic { get; set; }
        public string original_pic { get; set; }

        public bool favorited { get; set; }

        public int comments_count
        {
            get;
            set;
        }
        public int reposts_count
        {
            get;
            set;
        }
        public int thumb_pic_width { get; set; }
        public int thumb_pic_height { get; set; }

        public UserExt user { get; set; }
        public WeiboStatus retweeted_status { get; set; }

        #region

        public string topic { get; set; }
        public string widget { get; set; }

        public bool has_pic { get; set; }

        public WeiboTopicSource topic_source { get; set; }

        public string description { get; set; }

        public bool has_rt { get { return retweeted_status != null; } }

        public int reposts_comments_count { get; set; }
        public WeiboUrl url { get; set; }
        public WeiboEditor editor { get { return _editor; } set { Set(ref _editor, value); } }
        public CommentsViewModel comments { get { return _comments; } set { Set(ref _comments, value); } }
        public ICommand show_editor { get; set; }
        public ICommand show_comments { get; set; }
        public ICommand show_retweeted_comments { get; set; }

        #endregion

        #region constructor
        public WeiboStatus()
        {
            url = new WeiboUrl();
            show_editor = new RelayCommand(execute_show_editor);
            show_comments = new RelayCommand<IWeiboAccessToken>(execute_show_comments);
            show_retweeted_comments = new RelayCommand<IWeiboAccessToken>(execute_show_retweeted_comments);
        }
        void execute_show_retweeted_comments(IWeiboAccessToken o)
        {
            Debug.Assert(o != null && retweeted_status != null);
            if(comments == null || comments.id != retweeted_status.id)
            {
                comments = new CommentsViewModel(retweeted_status.id);
                comments.initialize(o.get());
            }else
            {
                comments = null;
            }
        }
        private void execute_show_comments(IWeiboAccessToken o)
        {
            Debug.Assert(o != null);
            comments = comments == null ? new CommentsViewModel(id) : null;
            if (comments == null || (retweeted_status != null ? retweeted_status.id : 0) != comments.id )
            {
                comments = new CommentsViewModel(id);
                comments.initialize(o.get());
            }
            else
                comments = null;
        }
        private void execute_show_editor()
        {
            editor = _editor == null ? new WeiboEditor() : null;
        }

        #endregion
        public List<Token> tokens { get; set; }

        public void assign_sina_data(StatusWithoutRt data)
        {
            /*
            idstr	string	字符串型的微博ID
            created_at	string	创建时间
            id	int64	微博ID
            text	string	微博信息内容
            source	string	微博来源
            favorited	boolean	是否已收藏
            truncated	boolean	是否被截断
            in_reply_to_status_id	int64	回复ID
            in_reply_to_user_id	int64	回复人UID
            in_reply_to_screen_name	string	回复人昵称
            mid	int64	微博MID
            bmiddle_pic	string	中等尺寸图片地址
            original_pic	string	原始图片地址
            thumbnail_pic	string	缩略图片地址
            reposts_count	int	转发数
            comments_count	int	评论数
            annotations	array	微博附加注释信息
            geo	object	地理信息字段
            user */
            //var jt = ((JToken)data.created_at);

            created_at = time(data.created_at);
            id = data.id;
            text = data.text;
            favorited = data.favorited;
            mid = data.mid;
            bmiddle_pic = data.bmiddle_pic;
            thumbnail_pic = data.thumbnail_pic;
            original_pic = data.original_pic;
            reposts_count = data.reposts_count;
            comments_count = data.comments_count;
            user = new UserExt();
            user.assign_sina(data.user);
            
            has_pic = !string.IsNullOrEmpty(bmiddle_pic);            
            reposts_comments_count = comments_count + reposts_count;
        }
        public void assign_sina(Status data)
        {
            assign_sina_data(data);
            var para = new InitializeParam();
            if(data.retweeted_status != null)
            {
                retweeted_status = new WeiboStatus();
                retweeted_status.assign_sina_data(data.retweeted_status);
                retweeted_status.post_initialize(false,para);
            }
            post_initialize(true, para);
            if (url.has_media)
                Messenger.Default.Send(url);
        }
        internal static DateTime time(string tm)
        {
            if (string.IsNullOrEmpty(tm))
                return DateTime.MinValue;
            const string format = "ddd MMM dd HH:mm:ss zzzz yyyy"; //"ddd MMM dd HH:mm:ss zzzz yyyy";
            var tmt = DateTime.ParseExact(tm, format, new CultureInfo("en-US", true));
            return tmt;
        }

        void post_initialize(bool arrange_tokens , InitializeParam param)
        {
            tokens = text.Parse();
            var f = tokens.ElementAtOrDefault(0);
            Debug.Assert(f != null);

            //正文的第一行作为标题
            if(f.tag == TokenTypes.Part && f.text.Length < 20)//不要很长的标题
            {
                topic_source = WeiboTopicSource.FirstSentence;
                topic = f.text.Trim();
            }
           // var url = string.Empty;
            //正文有标题
            foreach(var t in tokens)
            {
                if (t.tag == TokenTypes.Topic)
                {
                    topic = t.text;
                    topic_source = WeiboTopicSource.Trend;
                    //break;
                }
                if(t.tag == TokenTypes.Quote && topic_source == WeiboTopicSource.Reserved)
                {
                    topic_source = WeiboTopicSource.Quote;
                    topic = t.text.Trim();
                }
                if(t.tag == TokenTypes.Hyperlink && t.text.Contains("http://t.cn/"))
                {
                    if (param.url == null)
                        param.url = t.text;
                }
            }
            if(retweeted_status != null)
            {
                //使用rt中的标题
                if(retweeted_status.topic_source != WeiboTopicSource.Reserved)
                {
                    topic_source = WeiboTopicSource.Retweeted;
                    topic = retweeted_status.topic;
                }

                if (retweeted_status.has_pic && !has_pic)
                {
                    has_pic = true;
                    bmiddle_pic = retweeted_status.bmiddle_pic;
                    thumbnail_pic = retweeted_status.thumbnail_pic;

                    thumb_pic_width = retweeted_status.thumb_pic_width;
                    thumb_pic_height = retweeted_status.thumb_pic_height;
                }
                
            }

            //在使用正文首行作为标题前，如果有url的标题，就用url标题
            if(!string.IsNullOrEmpty(param.url))
                fetch_url_info(param.url);

            if (arrange_tokens == false)
                return;

            if(retweeted_status != null)
            {
                if(tokens.Count > 0)
                    tokens.Insert(0,new Token{tag = TokenTypes.Writer, text = user.screen_name});
                tokens.Insert(0, new Token { tag = TokenTypes.Break });
                tokens.Insert(0, new Token { tag = TokenTypes.Break });
                tokens.InsertRange(0,retweeted_status.tokens);
            }

            //从正文中删除标题内容
            if (tokens.Count > 0 && topic == tokens[0].text)
            {
                tokens.RemoveAt(0);//去掉第一句
            }
            //去除正文开始的标点
            while (tokens.Count > 0)
            {
                var tag =tokens[0].tag;
                if ( tag== TokenTypes.Punctuation || tag == TokenTypes.Break)
                    tokens.RemoveAt(0);
                else
                    break;
            }
        }

        void fetch_url_info(string uri)
        {
            var mem = MemoryCache.Default;
            var ui = (UrlInfo) mem.Get(uri);
            if (ui == null)
                return;
            url.assign(ui);
            var tpc = url.topic;
            if (!string.IsNullOrEmpty(tpc) && !tpc.Contains("..."))//标题可能被urlshort截断
            {
                topic = tpc;
                topic_source = WeiboTopicSource.UrlContent;
            }
            if(string.IsNullOrEmpty(bmiddle_pic) && !string.IsNullOrEmpty(url.pic))
            {
                bmiddle_pic = url.pic;
                thumbnail_pic = url.pic;
                has_pic = true;
            }
        }
        protected  class InitializeParam
        {
            public string url { get; set; }
        }
    }
}
