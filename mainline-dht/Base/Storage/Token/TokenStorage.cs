using FluentScheduler;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Storage.Token
{
    class TokenStorage
    {
        public int TokenExpirationInSecs { get; private set; }

        private object __tklock;
        private Dictionary<ulong, Token> _tokens;
        private List<Token> _tokensByExpTime;

        public TokenStorage(int tokenExpirationInSecs = 600) {
            TokenExpirationInSecs = tokenExpirationInSecs;
            __tklock = new object();
            _tokensByExpTime = new List<Token>();
            _tokens = new Dictionary<ulong, Token>();
            JobManager.AddJob(ClearExpiredTokens, (sch) => sch.ToRunEvery(TokenExpirationInSecs).Seconds());
        }

        public bool Validate(IPAddress acqAddress, Token tokenToValidate) {
            ulong tokenId = CalcTokenId(tokenToValidate);

            Token storageToken = null;
            lock(__tklock) {
                if(_tokens.ContainsKey(tokenId)) {
                    storageToken = _tokens[tokenId];
                } else {
                    return false;
                }
            }

            if((DateTime.UtcNow - storageToken.Timestamp).TotalSeconds >= TokenExpirationInSecs) {
                ClearExpiredTokens();
                return false;
            }

            return true;
        }

        public Token AcquireNewToken(IPAddress forIp) {
            Token token = new Token(forIp);
            lock(__tklock) {
                _tokensByExpTime.Add(token);
                insSortTokensByExpTime();
                _tokens.Add(CalcTokenId(token), token);
            }

            return token;
        }

        private ulong CalcTokenId(Token token) {
            ulong tokenId = 0;
            byte[] addressBytes = token.Address.GetAddressBytes();
            for(int i = 8; i < 36; i += 8) {
                tokenId |= addressBytes[(i / 8) - 1];
                tokenId = tokenId << i;
            }

            tokenId = tokenId << 32;
            tokenId |= (uint)token.Secret;

            return tokenId;
        }

        private void insSortTokensByExpTime() {
            for(int i = 0; i < _tokensByExpTime.Count - 1; i++) {
                for(int j = i + 1; j > 0; j--) {
                    if(_tokensByExpTime[j - 1].Timestamp > _tokensByExpTime[j].Timestamp) {
                        Token temp = _tokensByExpTime[j - 1];
                        _tokensByExpTime[j - 1] = _tokensByExpTime[j];
                        _tokensByExpTime[j] = temp;
                    }
                }
            }
        }

        private int LowerBound(DateTime fromDate) {
            int hi = _tokensByExpTime.Count, lo = 0;

            while(lo < hi) {
                int mid = (hi + lo) / 2;
                if(_tokensByExpTime[mid].Timestamp > fromDate) {
                    hi = mid;
                } else {
                    lo = mid + 1;
                }
            }

            return lo - 1;
        }

        private void ClearExpiredTokens() {
            if(_tokensByExpTime.Count == 0) {
                return;
            }

            lock(__tklock) {
                int rightMost = LowerBound(DateTime.UtcNow.AddSeconds(-TokenExpirationInSecs));

                for(int i = rightMost; i < _tokensByExpTime.Count; i++) {
                    _tokens.Remove(CalcTokenId(_tokensByExpTime[i]));
                }

                _tokensByExpTime = _tokensByExpTime.GetRange(0, _tokensByExpTime.Count - rightMost);
            }
        }
    }
}
