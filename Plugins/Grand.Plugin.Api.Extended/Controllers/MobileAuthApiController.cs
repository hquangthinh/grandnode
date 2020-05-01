using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Grand.Api.Interfaces;
using Grand.Core.Domain.Customers;
using Grand.Services.Authentication;
using Grand.Services.Authentication.External;
using Grand.Services.Customers;
using Grand.Services.Security;
using Grand.Web.Areas.Api.Controllers;
using Grand.Web.Areas.Api.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended.Controllers
{
    public class MobileLoginModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public LoginResponse()
        {
            Claims = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }
        public string AppAccessToken { get; set; }
        public string FirebaseAuthToken { get; set; }

        public Dictionary<string, object> Claims { get; set; }
    }

    public class IdTokenVerificationTokenCommand
    {
        /// <summary>
        /// ExternalAuth.Google or ExternalAuth.Facebook
        /// </summary>
        public string ProviderSystemName { get; set; }
        public string ProviderAccessToken { get; set; }
        /// <summary>
        /// This is Google account id or Facebook profile id
        /// </summary>
        public string ProviderUserId { get; set; }
        public string FirebaseIdToken { get; set; }
        public string FirebaseUserId { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
    }

    public class IdTokenVerificationResponse
    {
        public bool IsSuccess { get; set; }
        public string AppAccessToken { get; set; }

        public string Error { get; set; }

        public static IdTokenVerificationResponse Fail(string error) => new IdTokenVerificationResponse { IsSuccess = false, Error = error };
    }

    [Route("api/mobile-auth")]
    public class MobileAuthApiController : BaseApiController
    {
        private readonly IGrandAuthenticationService _authenticationService;
        private readonly IExternalAuthenticationService _externalAuthenticationService;
        private readonly ICustomerService _customerService;
        private readonly CustomerSettings _customerSettings;
        private readonly ITokenService _tokenService;
        private readonly IEncryptionService _encryptionService;
        private readonly ApiExtendedSettings _apiExtendedSettings;
        public MobileAuthApiController(ITokenService tokenService
            , IGrandAuthenticationService authenticationService
            , IExternalAuthenticationService externalAuthenticationService
            , ICustomerService customerService
            , CustomerSettings customerSettings
            , IEncryptionService encryptionService
            , ApiExtendedSettings apiExtendedSettings
        )
        {
            _tokenService = tokenService;
            _authenticationService = authenticationService;
            _externalAuthenticationService = externalAuthenticationService;
            _customerService = customerService;
            _customerSettings = customerSettings;
            _encryptionService = encryptionService;
            _apiExtendedSettings = apiExtendedSettings;
        }

        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [HttpPost]
        [Route("token")]
        public async Task<IActionResult> CreateToken([FromBody] MobileLoginModel model)
        {
            var passwordMatch = false;
            var userId = "";
            var displayName = "";
            if (!string.IsNullOrEmpty(model.Email))
            {
                var customer = await _customerService.GetCustomerByEmail(model.Email.ToLowerInvariant());
                if (customer != null && customer.Active && !customer.IsSystemAccount)
                {
                    var base64EncodedBytes = Convert.FromBase64String(model.Password);
                    var password = Encoding.UTF8.GetString(base64EncodedBytes);
                    if(_customerSettings.DefaultPasswordFormat == PasswordFormat.Hashed)
                    {
                        passwordMatch = customer.Password == _encryptionService.CreatePasswordHash(password, customer.PasswordSalt, _customerSettings.HashedPasswordFormat);
                    } 
                    else if(_customerSettings.DefaultPasswordFormat == PasswordFormat.Encrypted)
                    {
                        passwordMatch = customer.Password == _encryptionService.EncryptText(password);
                    } 
                    else
                    {
                        passwordMatch = customer.Password == password;
                    }
                    userId = customer.Id;
                    displayName = customer.GetFullName();
                }
            }

            if(!passwordMatch)
            {
                return Ok(new LoginResponse { IsSuccess = false });
            }

            var claims = new Dictionary<string, string> {
                { "Email", model.Email },
                { "UserId", userId },
                { "DisplayName", displayName }
            };

            var result = new LoginResponse {
                IsSuccess = true
            };

            try
            {
                result.AppAccessToken = await _tokenService.GenerateToken(claims);

                EnsureFirebaseAppInstance();

                result.Claims = new Dictionary<string, object> 
                {
                    { "Email", model.Email },
                    { "UserId", userId },
                    { "DisplayName", displayName }
                };

                result.FirebaseAuthToken = await FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(userId, result.Claims);
            }
            catch 
            {
                result.IsSuccess = false;
            }

            return Ok(result);
        }

        [HttpPost]
        [Route("signout")]
        public async Task<IActionResult> SignOut()
        {
            await _authenticationService.SignOut();
            return Ok();
        }


        private void EnsureFirebaseAppInstance()
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions() {
                    Credential = GoogleCredential.FromFile(_apiExtendedSettings.FirebaseAppCredentialConfigurationFile)
                });
            }
        }

        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [HttpPost]
        [Route("verify-firebase-id-token")]
        public async Task<IActionResult> VerifyFirebaseIdToken([FromBody] IdTokenVerificationTokenCommand command)
        { 
            if(command == null
                || string.IsNullOrEmpty(command.ProviderSystemName)
                || string.IsNullOrEmpty(command.ProviderAccessToken)
                || string.IsNullOrEmpty(command.FirebaseIdToken) 
                || string.IsNullOrEmpty(command.FirebaseUserId)
                || string.IsNullOrEmpty(command.Email)
                || string.IsNullOrEmpty(command.DisplayName)
            )
            {
                return Ok(IdTokenVerificationResponse.Fail("MobileAuthApiController.VerifyFirebaseIdToken.invalid_parameter"));
            }

            var result = new IdTokenVerificationResponse { IsSuccess = false };

            try
            {
                EnsureFirebaseAppInstance();

                var verifyResult = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(command.FirebaseIdToken);

                if(verifyResult == null)
                {
                    result.Error = "Cannot verify id token";
                    return Ok(result);
                }

                if (verifyResult.Claims.TryGetValue("Email", out var claimEmail))
                {
                    if (string.Compare(claimEmail.ToString(), command.Email, ignoreCase: true) != 0)
                    {
                        result.Error = "Email does not exist in id token claim";
                        return Ok(result);
                    }
                }

                var customer = await _customerService.GetCustomerByEmail(command.Email.ToLowerInvariant());
                if (customer != null && (!customer.Active || customer.IsSystemAccount))
                {
                    result.Error = "Invalid customer";
                    return Ok(result);
                }

                if(customer == null)
                {
                    // link customer with firebase user
                    var authenticationParameters = new ExternalAuthenticationParameters {
                        ProviderSystemName = command.ProviderSystemName,
                        AccessToken = command.ProviderAccessToken,
                        Email = command.Email,
                        ExternalIdentifier = command.ProviderUserId,
                        ExternalDisplayIdentifier = command.DisplayName,
                        Claims = new List<ExternalAuthenticationClaim>()
                        {
                            new ExternalAuthenticationClaim("FirebaseUserId", command.FirebaseUserId),
                            new ExternalAuthenticationClaim("Email", command.Email),
                            new ExternalAuthenticationClaim("DisplayName", command.DisplayName),
                        }
                    };
                    var appAuthResult = await _externalAuthenticationService.Authenticate(authenticationParameters);
                    if(appAuthResult is RedirectToActionResult res)
                    {
                        if(res.ActionName == "Login")
                        {
                            return Ok(IdTokenVerificationResponse.Fail("MobileAuthApiController.VerifyFirebaseIdToken.failed_external_auth_check"));
                        }
                    }
                }

                var claims = new Dictionary<string, string> 
                {
                    { "Email", command.Email },
                    { "UserId", command.FirebaseUserId },
                    { "DisplayName", command.DisplayName }
                };
                
                var token = await _tokenService.GenerateToken(claims);

                result.IsSuccess = true;
                result.AppAccessToken = token;
            }
            catch
            {
                return Ok(IdTokenVerificationResponse.Fail("MobileAuthApiController.VerifyFirebaseIdToken.fail_to_verify_id_token"));
            }

            return Ok(result);
        }
    }
}
