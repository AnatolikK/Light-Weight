using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;

namespace Project
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var random = new Random();
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.GetConnectionString("myConxStr");
            builder.Services.AddAuthorization();
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();
            var connection = builder.Configuration.GetConnectionString("dataBase");
            var credentials = new NetworkCredential("digitalportfoliolw@mail.ru", builder.Configuration.GetConnectionString("emailPassword"));
            var app = builder.Build();
            app.UseAuthorization();
            app.UseAuthentication();
            app.UseStaticFiles();
            app.UseRouting();

            app.MapGet("/", async context =>
            {
                context.Response.ContentType = "text/html";
                if (context.User.Identity?.IsAuthenticated ?? false)
                    await context.Response.SendFileAsync("wwwroot/MainPageForAuthorized.html");
                else
                    await context.Response.SendFileAsync("wwwroot/MainPageForUnauthorized.html");
            });

            app.MapGet("/getuserinfo", async context => await context.Response.WriteAsJsonAsync(new { username = context.User.FindFirstValue(ClaimTypes.NameIdentifier) }));

            app.MapGet("/registration", async context =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? false)
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync("wwwroot/Registration.html");
                }
            });

            app.MapPost("/sendemail", async context =>
            {
                var emailData = await context.Request.ReadFromJsonAsync<EmailData>();
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.Connection = con;
                command.CommandText = "SELECT Count(*) " +
                                      "FROM [users] " +
                                      "WHERE username=@username";
                command.Parameters.AddWithValue("username", emailData.Username);
                var res = await command.ExecuteScalarAsync();
                if ((int)res == 1)
                    context.Response.StatusCode = 400;

                else if ((int)res == 0)
                {
                    var message = new MailMessage("digitalportfoliolw@mail.ru", emailData!.Email)
                    {
                        Subject = "Код для подтверждения регистрации",
                        Body = emailData.Code
                    };

                    var client = new SmtpClient("smtp.mail.ru", 587)
                    {
                        EnableSsl = true,
                        Credentials = credentials
                    };

                    await client.SendMailAsync(message);
                }

                await con.CloseAsync();
            });

            app.MapPost("/register", async context =>
            {
                var data = await context.Request.ReadFromJsonAsync<UserData>();
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.Connection = con;
                command.CommandText = "INSERT INTO [users] (username, mail, password, creationdate) " +
                                      "VALUES (@username, @mail, @password, @creationdate)";
                command.Parameters.AddWithValue("username", data!.Username);
                command.Parameters.AddWithValue("mail", data.Email);
                command.Parameters.AddWithValue("password", data.Password);
                command.Parameters.AddWithValue("creationdate", DateTime.Today.ToString("dd-MM-yyyy"));
                await command.ExecuteNonQueryAsync();
                await con.CloseAsync();
                var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, data.Username) };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                await con.CloseAsync();
            });

            app.MapGet("/unregister", async context =>
            {
                if (context.User.Identity?.IsAuthenticated ?? false)
                    await context.SignOutAsync();
            });

            app.MapGet("/signing", async context =>
            {
                if (!context.User.Identity?.IsAuthenticated ?? false)
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync("wwwroot/EnteringAccount.html");
                }
            });

            app.MapPost("/signin", async context =>
            {
                var userData = await context.Request.ReadFromJsonAsync<UserData>();
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.Connection = con;
                command.CommandText = "SELECT count(*) " +
                                      "FROM [users] " +
                                      "WHERE username=@username AND password=@password";
                command.Parameters.AddWithValue("username", userData!.Username);
                command.Parameters.AddWithValue("password", userData.Password);
                var result = await command.ExecuteScalarAsync();
                if ((int)result! == 1)
                {
                    var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userData.Username) };
                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                }

                else if ((int)result! == 0)
                    context.Response.StatusCode = 404;
                await con.CloseAsync();
            });

            app.MapGet("/{username}", async (string username, HttpContext context) =>
            {
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.CommandText = "SELECT Count(*) " +
                                      "FROM [users] " +
                                      "WHERE username=@username";
                command.Connection = con;
                command.Parameters.AddWithValue("username", username);
                var result = await command.ExecuteScalarAsync();
                if ((int)result == 1)
                {
                    context.Response.ContentType = "text/html";
                    var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (user == username)
                        await context.Response.SendFileAsync("wwwroot/ProfileForOwner.html");
                    else
                        await context.Response.SendFileAsync("wwwroot/ProfileForNotOwner.html");
                }

                else if ((int)result == 0)
                    context.Response.StatusCode = 404;
                await con.CloseAsync();
            });

            app.MapGet("/{username}/getinformation", async (string username, HttpContext context) =>
            {
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.CommandText = "SELECT [mail], [picture], [description], [creationdate] " +
                                      "FROM [users] " +
                                      "WHERE username=@username";
                command.Connection = con;
                command.Parameters.AddWithValue("username", username);
                var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var email = reader.GetString(0);
                    var picture = await reader.IsDBNullAsync(1) ? null : Convert.ToBase64String((byte[])reader.GetValue(1));
                    var description = await reader.IsDBNullAsync(2) ? null : reader.GetString(2);
                    var creationdate = reader.GetString(3);
                    await reader.CloseAsync();
                    command.CommandText = "SELECT [nameofportfolio], [description], [creationdate] " +
                                          "FROM [portfolios] " +
                                          "WHERE nameofauthor=@username";
                    var readerPortfolios = await command.ExecuteReaderAsync();
                    var portfolios = new List<ProfilePortfolio>();
                    while (await readerPortfolios.ReadAsync())
                    {
                        var namePortfolio = readerPortfolios.GetString(0);
                        var descriptionPortfolio = readerPortfolios.GetString(1);
                        var creationDatePortfolio = readerPortfolios.GetString(2);
                        var profilePortfolio = new ProfilePortfolio(namePortfolio, descriptionPortfolio, creationDatePortfolio);
                        portfolios.Add(profilePortfolio);
                    }

                    var profile = new ProfileInformation(username, description, creationdate, email, picture, portfolios.ToArray());
                    await context.Response.WriteAsJsonAsync(profile);
                }

                else
                    context.Response.StatusCode = 404;
                await con.CloseAsync();
            });

            app.MapPost("/{username}/changeinformation", async (string username, HttpContext context) =>
            {
                var data = await context.Request.ReadFromJsonAsync<ProfileChangeInformation>();
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.CommandText = data.Avatar == null
                                      ? "UPDATE [users] " +
                                      "SET description=@description " +
                                      "WHERE username=@username"
                                      : "UPDATE [users] " +
                                      "SET description=@description, picture=@picture " +
                                      "WHERE username=@username";
                command.Connection = con;
                command.Parameters.AddWithValue("username", username);
                command.Parameters.AddWithValue("description", data.Description);
                if (data.Avatar != null)
                {
                    var base64arr = data.Avatar.Split(',')[1];
                    command.Parameters.AddWithValue("picture", Convert.FromBase64String(base64arr));
                }

                await command.ExecuteNonQueryAsync();
                await con.CloseAsync();
            });

            app.MapGet("/{username}/createportfolio", async (string username, HttpContext context) =>
            {
                if (context.User.Identity?.IsAuthenticated ?? false && context.User.FindFirstValue(ClaimTypes.NameIdentifier) == username)
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync("wwwroot/CreatingPortfolio.html");
                }

                else
                    context.Response.StatusCode = 401;
            });

            app.MapPost("/{username}/createportfolio/addportfolio", async (string username, HttpContext context) =>
            {
                var data = await context.Request.ReadFromJsonAsync<PortfolioInformation>();
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.Connection = con;
                command.CommandText = "SELECT Count(*) " +
                                      "FROM [portfolios] " +
                                      "WHERE nameofauthor=@nameofauthor AND nameofportfolio=@nameofportfolio";
                command.Parameters.AddWithValue("nameofauthor", username);
                command.Parameters.AddWithValue("nameofportfolio", data.NameOfPortfolio);
                var result = await command.ExecuteScalarAsync();
                if ((int)result == 1)
                    context.Response.StatusCode = 400;
                else if ((int)result == 0)
                {
                    command.CommandText = "INSERT INTO [portfolios] " +
                                          "(nameofportfolio, workfile, description, creationdate, nameofauthor, mimetype) " +
                                          "VALUES (@nameofportfolio, @workfile, @description, @creationdate, @nameofauthor, @mimetype)";
                    command.Parameters.AddWithValue("description", data.Description);
                    command.Parameters.AddWithValue("creationdate", DateTime.Today.ToString("dd-MM-yyyy"));
                    var file = data.WorkFile != null ? data.WorkFile.Split(',') : new string[2];
                    var workFile = data.WorkFile != null ? Convert.FromBase64String(file[1]) : null;
                    var mimeType = data.WorkFile != null ? file[0] + "," : null;
                    command.Parameters.Add("@workfile", System.Data.SqlDbType.VarBinary).Value = workFile ?? (object)DBNull.Value;
                    command.Parameters.AddWithValue("mimetype", mimeType ?? (object)DBNull.Value);
                    await command.ExecuteNonQueryAsync();
                }

                await con.CloseAsync();
            });

            app.MapGet("/{username}/{portfolio}", async (string username, string portfolio, HttpContext context) =>
            {
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.Connection = con;
                command.CommandText = "SELECT Count(*) " +
                                      "FROM [portfolios] " +
                                      "WHERE nameofportfolio=@nameofportfolio AND nameofauthor=@nameofauthor";
                command.Parameters.AddWithValue("nameofportfolio", portfolio);
                command.Parameters.AddWithValue("nameofauthor", username);
                var result = await command.ExecuteScalarAsync();
                if ((int)result == 1)
                {
                    context.Response.ContentType = "text/html";
                    if (context.User.Identity?.IsAuthenticated ?? false)
                    {
                        if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) == username)
                            await context.Response.SendFileAsync("wwwroot/PortfolioForOwner.html");
                        else
                            await context.Response.SendFileAsync("wwwroot/PortfolioForAuthorized.html");
                    }

                    else
                        await context.Response.SendFileAsync("wwwroot/PortfolioForUnauthorized.html");
                }

                else if ((int)result == 0)
                    context.Response.StatusCode = 404;
                await con.CloseAsync();
            });

            app.MapGet("/{username}/{portfolio}/getinformation", async (string username, string portfolio, HttpContext context) =>
            {
                var portfolioInformation = new PortfolioInformation();
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.Connection = con;
                command.CommandText = "SELECT [workfile], [description], [creationdate], [mimetype] " +
                                      "FROM [portfolios] " +
                                      "WHERE nameofportfolio=@nameofportfolio AND nameofauthor=@nameofauthor";
                command.Parameters.AddWithValue("nameofportfolio", portfolio);
                command.Parameters.AddWithValue("nameofauthor", username);
                var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    portfolioInformation.Description = reader.GetString(1);
                    portfolioInformation.CreationDate = reader.GetString(2);
                    if (!await reader.IsDBNullAsync(0) && !await reader.IsDBNullAsync(3))
                        portfolioInformation.WorkFile = reader.GetString(3) + Convert.ToBase64String((byte[])reader.GetValue(0));
                    await reader.CloseAsync();

                    command.CommandText = "SELECT Sum(rated) " +
                                          "FROM [rating] " +
                                          "WHERE nameofportfolio=@nameofportfolio AND portfolioauthor=@nameofauthor";
                    var result = await command.ExecuteScalarAsync();
                    portfolioInformation.Rating = result == DBNull.Value ? 0 : (int)result;

                    command.CommandText = "SELECT [authorname], [text], [creationdate], [id] " +
                                          "FROM [commentaries] " +
                                          "WHERE nameofportfolio=@nameofportfolio AND portfolioauthor=@nameofauthor";
                    var readerComs = await command.ExecuteReaderAsync();
                    var commentaries = new List<CommentaryInfo>();
                    while (await readerComs.ReadAsync())
                    {
                        var com = new CommentaryInfo();
                        com.AuthorName = readerComs.GetString(0);
                        com.Text = readerComs.GetString(1);
                        com.CreationDate = readerComs.GetString(2);
                        com.Id = readerComs.GetInt32(3);
                        com.IsOwner = com.AuthorName == context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                        commentaries.Add(com);
                    }

                    portfolioInformation.Commentaries = commentaries.ToArray();
                    await context.Response.WriteAsJsonAsync(portfolioInformation);
                }

                else
                    context.Response.StatusCode = 404;
                await con.CloseAsync();
            });

            app.MapPost("/{username}/{portfolio}/rate", async (string username, string portfolio, HttpContext context) =>
            {
                string rate = "";
                var authorName = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (authorName == null)
                    context.Response.StatusCode = 400;
                else
                {
                    using (var stream = new StreamReader(context.Request.Body))
                    {
                        string body = await stream.ReadToEndAsync();
                        rate = body;
                    }

                    using var con = new SqlConnection(connection);
                    await con.OpenAsync();
                    var command = new SqlCommand();
                    command.Connection = con;
                    command.CommandText = "SELECT [rated] " +
                                          "FROM [rating] " +
                                          "WHERE portfolioauthor=@portfolioauthor AND nameofportfolio=@nameofportfolio AND authorname=@authorname";
                    command.Parameters.AddWithValue("portfolioauthor", username);
                    command.Parameters.AddWithValue("nameofportfolio", portfolio);
                    command.Parameters.AddWithValue("authorname", authorName);
                    var result = await command.ExecuteScalarAsync();
                    if (result is not null && (short)result == 1)
                    {
                        if (rate == "+")
                        {
                            command.CommandText = "DELETE FROM [rating] " +
                                                  "WHERE portfolioauthor=@portfolioauthor AND nameofportfolio=@nameofportfolio AND authorname=@authorname";
                            await command.ExecuteNonQueryAsync();
                            await context.Response.WriteAsync("Лайк убран");
                        }

                        else if (rate == "-")
                        {
                            command.CommandText = "UPDATE [rating] " +
                                                  "SET rated=@rated " +
                                                  "WHERE portfolioauthor=@portfolioauthor AND nameofportfolio=@nameofportfolio AND authorname=@authorname";
                            command.Parameters.AddWithValue("rated", -1);
                            await command.ExecuteNonQueryAsync();
                            await context.Response.WriteAsync("Дизлайк поставлен");
                        }
                    }

                    else if (result is not null && (short)result == -1)
                    {
                        if (rate == "+")
                        {
                            command.CommandText = "UPDATE [rating] " +
                                                  "SET rated=@rated " +
                                                  "WHERE portfolioauthor=@portfolioauthor AND nameofportfolio=@nameofportfolio AND authorname=@authorname";
                            command.Parameters.AddWithValue("rated", 1);
                            await command.ExecuteNonQueryAsync();
                            await context.Response.WriteAsync("Лайк поставлен");
                        }

                        else if (rate == "-")
                        {
                            command.CommandText = "DELETE FROM [rating] " +
                                                  "WHERE portfolioauthor=@portfolioauthor AND nameofportfolio=@nameofportfolio AND authorname=@authorname";
                            await command.ExecuteNonQueryAsync();
                            await context.Response.WriteAsync("Дизлайк убран");
                        }
                    }

                    else
                    {
                        command.CommandText = "INSERT INTO [rating] " +
                                              "(authorname, nameofportfolio, rated, portfolioauthor) " +
                                              "VALUES (@authorname, @nameofportfolio, @rated, @portfolioauthor)";
                        var rated = rate == "+" ? 1 : -1;
                        command.Parameters.AddWithValue("rated", rated);
                        await command.ExecuteNonQueryAsync();
                        if (rated == 1)
                            await context.Response.WriteAsync("Лайк поставлен");
                        else if (rated == -1)
                            await context.Response.WriteAsync("Дизлайк поставлен");
                    }

                    await con.CloseAsync();
                }
            });

            app.MapGet("/{username}/{portfolio}/delete", async (string username, string portfolio, HttpContext context) =>
            {
                if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) != username)
                    context.Response.StatusCode = 401;
                else
                {
                    using var con = new SqlConnection(connection);
                    await con.OpenAsync();
                    var command = new SqlCommand();
                    command.Parameters.AddWithValue("nameofportfolio", portfolio);
                    command.Parameters.AddWithValue("nameofauthor", username);
                    command.Connection = con;

                    command.CommandText = "DELETE FROM [portfolios] " +
                                          "WHERE nameofportfolio=@nameofportfolio AND nameofauthor=@nameofauthor";
                    await command.ExecuteNonQueryAsync();

                    command.CommandText = "DELETE FROM [rating] " +
                                          "WHERE nameofportfolio=@nameofportfolio AND portfolioauthor=@nameofauthor";
                    await command.ExecuteNonQueryAsync();

                    command.CommandText = "DELETE FROM [commentaries] " +
                                          "WHERE nameofportfolio=@nameofportfolio AND portfolioauthor=@nameofauthor";
                    await command.ExecuteNonQueryAsync();

                    await con.CloseAsync();
                }
            });

            app.MapPost("{username}/{portfolio}/comment", async (string username, string portfolio, HttpContext context) =>
            {
                var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (user == null)
                    context.Response.StatusCode = 401;
                else
                {
                    var text = "";
                    using (var stream = new StreamReader(context.Request.Body))
                    {
                        string body = await stream.ReadToEndAsync();
                        text = body;
                    }

                    using var con = new SqlConnection(connection);
                    await con.OpenAsync();
                    var command = new SqlCommand();
                    command.Connection = con;
                    command.CommandText = "INSERT INTO [commentaries] " +
                                          "(authorname, nameofportfolio, text, creationdate, portfolioauthor) " +
                                          "VALUES (@authorname, @nameofportfolio, @text, @creationdate, @portfolioauthor)";
                    command.Parameters.AddWithValue("authorname", user);
                    command.Parameters.AddWithValue("nameofportfolio", portfolio);
                    command.Parameters.AddWithValue("text", text);
                    command.Parameters.AddWithValue("creationdate", DateTime.Today.ToString("dd-MM-yyyy"));
                    command.Parameters.AddWithValue("portfolioauthor", username);
                    await command.ExecuteNonQueryAsync();
                    await con.CloseAsync();
                }
            });

            app.MapGet("{username}/{portfolio}/{commentid}/delete", async (int commentid, HttpContext context) =>
            {
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.Connection = con;
                command.CommandText = "SELECT [authorname] " +
                                      "FROM [commentaries] " +
                                      "WHERE id=@id";
                command.Parameters.AddWithValue("id", commentid);
                var result = await command.ExecuteScalarAsync();
                if (result == null)
                    context.Response.StatusCode = 404;
                else if ((string)result != context.User.FindFirstValue(ClaimTypes.NameIdentifier))
                    context.Response.StatusCode = 401;
                else
                {
                    command.CommandText = "DELETE FROM [commentaries] " +
                                          "WHERE id=@id";
                    await command.ExecuteNonQueryAsync();
                }

                await con.CloseAsync();
            });

            app.MapGet("/search", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.SendFileAsync("wwwroot/SearchPage.html");
            });

            app.MapPost("/search/getinfo", async context =>
            {
                var parameters = await context.Request.ReadFromJsonAsync<SearchParameters>();
                using var con = new SqlConnection(connection);
                await con.OpenAsync();
                var command = new SqlCommand();
                command.Connection = con;
                if (parameters.Type == "user")
                {
                    command.CommandText = "SELECT [username], [description], [creationdate] " +
                                          "FROM [users] " +
                                          $"WHERE username LIKE '%' + @username + '%'";
                    command.Parameters.AddWithValue("username", parameters.Name);
                    var reader = await command.ExecuteReaderAsync();
                    var accounts = new List<ProfileInformation>();
                    while (await reader.ReadAsync())
                    {
                        var username = reader.GetString(0);
                        var description = await reader.IsDBNullAsync(1) ? "" : reader.GetString(1);
                        var creationdate = reader.GetString(2);
                        accounts.Add(new ProfileInformation(username, description, creationdate));
                    }

                    await context.Response.WriteAsJsonAsync(accounts);
                }

                else if (parameters.Type == "portfolio")
                {
                    command.CommandText = "SELECT [nameofportfolio], [nameofauthor], [description], [creationdate] " +
                                          "FROM [portfolios] " +
                                          $"WHERE nameofportfolio LIKE '%' + @nameofportfolio + '%'";
                    command.Parameters.AddWithValue("nameofportfolio", parameters.Name);
                    var reader = await command.ExecuteReaderAsync();
                    var portfolios = new List<PortfolioInformation>();
                    while (await reader.ReadAsync())
                    {
                        var nameofportfolio = reader.GetString(0);
                        var nameofauthor = reader.GetString(1);
                        var description = await reader.IsDBNullAsync(2) ? "" : reader.GetString(2);
                        var creationdate = reader.GetString(3);
                        var portfolio = new PortfolioInformation() { NameOfPortfolio = nameofportfolio, AuthorName = nameofauthor, Description = description, CreationDate = creationdate };
                        portfolios.Add(portfolio);
                    }

                    await context.Response.WriteAsJsonAsync(portfolios);
                }

                else
                    context.Response.StatusCode = 400;
                await con.CloseAsync();
            });

            app.Run();
        }
    }
}