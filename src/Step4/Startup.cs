using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace authenticationlab
{
    public class Startup
    {
        private const string AccessTokenClaim = "urn:tokens:twitter:accesstoken";
        private const string AccessTokenSecret = "urn:tokens:twitter:accesstokensecret";

        private ILogger _logger;

        public Startup(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Startup>();
        }

        private static int RequestCount = 0;

        public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            if (context.Request.Path.HasValue && context.Request.Path == "/")
            {
                System.Threading.Interlocked.Increment(ref RequestCount);
            }

            if (RequestCount % 5 == 0)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = TwitterDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.SlidingExpiration = true;
                    options.ExpireTimeSpan = new System.TimeSpan(0, 5, 0);
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnValidatePrincipal = Startup.ValidateAsync
                    };
                })
                .AddTwitter(options =>
                {
                    options.ConsumerKey = "7eRsQzfm8fMIxmBV3zahlBaH5";
                    options.ConsumerSecret = "IPE7FxpiWmDmEEaxdjAb9nStm9nI5Q1LZtCUqCHjDDAqk5ISRi";
                    options.Events = new TwitterEvents()
                    {
                        OnRedirectToAuthorizationEndpoint = context =>
                        {
                            _logger.LogInformation("Redirecting to {0}", context.RedirectUri);
                            context.Response.Redirect(context.RedirectUri);
                            return Task.CompletedTask;
                        },
                        OnRemoteFailure = context =>
                        {
                            _logger.LogInformation("Something went horribly wrong.");
                            return Task.CompletedTask;
                        },
                        OnTicketReceived = context =>
                        {
                            _logger.LogInformation("Ticket recieved.");
                            return Task.CompletedTask;
                        },
                        OnCreatingTicket = context =>
                        {
                            _logger.LogInformation("Creating tickets.");
                            var identity = (ClaimsIdentity)context.Principal.Identity;
                            identity.AddClaim(new Claim(AccessTokenClaim, context.AccessToken));
                            identity.AddClaim(new Claim(AccessTokenSecret, context.AccessTokenSecret));
                            return Task.CompletedTask;
                        }
                    };
                });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseAuthentication();
            app.Run(async (context) =>
            {
                if (!context.User.Identity.IsAuthenticated)
                {
                    await context.ChallengeAsync();
                }
                context.Response.Headers.Add("Content-Type", "text/html");

                await context.Response.WriteAsync("<html><body>\r");

                var claimsIdentity = (ClaimsIdentity)context.User.Identity;
                var accessTokenClaim = claimsIdentity.Claims.FirstOrDefault(x => x.Type == AccessTokenClaim);
                var accessTokenSecretClaim = claimsIdentity.Claims.FirstOrDefault(x => x.Type == AccessTokenSecret);

                if (accessTokenClaim != null && accessTokenSecretClaim != null)
                {
                    var userCredentials = Tweetinvi.Auth.CreateCredentials(
                        "7eRsQzfm8fMIxmBV3zahlBaH5",
                        "IPE7FxpiWmDmEEaxdjAb9nStm9nI5Q1LZtCUqCHjDDAqk5ISRi",
                        accessTokenClaim.Value,
                        accessTokenSecretClaim.Value);

                    var authenticatedUser = Tweetinvi.User.GetAuthenticatedUser(userCredentials);
                    if (authenticatedUser != null && !string.IsNullOrWhiteSpace(authenticatedUser.ProfileImageUrlHttps))
                    {
                        await context.Response.WriteAsync(
                            string.Format("<img src=\"{0}\"></img>", authenticatedUser.ProfileImageUrlHttps));
                    }
                }

                await context.Response.WriteAsync("</body></html>\r");
            });
        }
    }
}