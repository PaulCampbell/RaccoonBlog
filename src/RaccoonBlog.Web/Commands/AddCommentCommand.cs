using System;
using System.Configuration;
using System.Web;
using RaccoonBlog.Web.Infrastructure.AutoMapper;
using RaccoonBlog.Web.Infrastructure.AutoMapper.Profiles.Resolvers;
using RaccoonBlog.Web.Infrastructure.Commands;
using RaccoonBlog.Web.Models;
using RaccoonBlog.Web.Services;
using RaccoonBlog.Web.ViewModels;
using Raven.Client;

namespace RaccoonBlog.Web.Commands
{
    public class AddCommentCommand : ICommand
    {
        private readonly CommentInput _commentInput;
        private readonly RequestValues _requestValues;
        private readonly int _postId;

        public IDocumentSession Session { get; set; }

        public AddCommentCommand(CommentInput commentInput, RequestValues requestValues, int postId)
        {
            _commentInput = commentInput;
            _requestValues = requestValues;
            _postId = postId;
        }

        public void Execute()
        {
            var post = Session.Load<Post>(_postId);
            var comments = Session.Load<PostComments>(_postId);

            var comment = new PostComments.Comment
            {
                Id = comments.GenerateNewCommentId(),
                Author = _commentInput.Name,
                Body = _commentInput.Body,
                CreatedAt = DateTimeOffset.Now,
                Email = _commentInput.Email,
                Url = _commentInput.Url,
                Important = _requestValues.IsAuthenticated,
                UserAgent = _requestValues.UserAgent,
                UserHostAddress = _requestValues.UserHostAddress
            };
            comment.IsSpam = new AskimetService(Session).CheckForSpam(comment);

            if (comment.IsSpam)
                comments.Spam.Add(comment);
            else
            {
                post.CommentsCount++;
                comments.Comments.Add(comment);
            }

			if (_requestValues.IsAuthenticated)
				return; // we don't send email for authenticated users

            SendNewCommentEmail(post, comment);
        }

    	private void SendNewCommentEmail(Post post, PostComments.Comment comment)
    	{
    		var viewModel = comment.MapTo<NewCommentEmailViewModel>();
    		viewModel.PostId = RavenIdResolver.Resolve(post.Id);
    		viewModel.PostTitle = post.Title;
    		viewModel.BlogName = Session.Load<BlogConfig>("Blog/Config").Title;

			var subject = string.Format("Comment on: {0} from {1}", viewModel.PostTitle, viewModel.BlogName);

			if(comment.IsSpam)
				subject = "Spam " + subject;

    		CommandExecutor.ExcuteLater(new SendEmailCommand(viewModel.Email,subject, "NewComment", viewModel));
    	}
    }
}
