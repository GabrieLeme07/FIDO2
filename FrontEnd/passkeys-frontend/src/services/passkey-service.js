import axios from 'axios';
import {
  create,
  get,
  parseCreationOptionsFromJSON,
  parseRequestOptionsFromJSON
} from '@github/webauthn-json/browser-ponyfill';
import sessionStorageService from './session-storage-service';
import { SessionConstants } from '../constants';

class PassKeyService {

  async checkCredential() {
    await this.waitForTokenRefresh();

    try {
      const bearerToken = sessionStorageService.get(SessionConstants.TokenKey);
      const response = await axios.get('https://localhost:7214/v1/Auth/check', {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${bearerToken}`
        }
      });

      return true;  

    } catch (error) {
      if (error.response.status === 400) {
        return false;
      } else {
        throw new Error('Login ERROR ' + error.message);
      }
    }
  }

  async createCredentialOptions() {
    await this.waitForTokenRefresh();

    const bearerToken = sessionStorageService.get(SessionConstants.TokenKey);
    const response = await axios.post('https://localhost:7214/v1/Auth/register/begin', null, {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${bearerToken}`
      }
    });
    if (response.status === 400) {
      throw new Error(response.data);
    }
    const credentialOptionsResponse = response.data;
    const options = credentialOptionsResponse.options;
    const abortController = new AbortController();

    return {
      options: parseCreationOptionsFromJSON({ publicKey: options, signal: abortController.signal }),
      userId: credentialOptionsResponse.userId
    };
  }

  async createCredentialOptionsForCurrentUser() {
    await this.waitForTokenRefresh();
    const bearerToken = sessionStorageService.get(SessionConstants.TokenKey);

    const response = await axios.put('https://localhost:7214/v1/Auth/register/begin', null, {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${bearerToken}`
      }
    });
    if (response.status === 400) {
      throw new Error(response.data);
    }
    const credentialOptionsResponse = response.data;
    const options = credentialOptionsResponse.options;
    const abortController = new AbortController();
    return {
      options: parseCreationOptionsFromJSON({ publicKey: options, signal: abortController.signal }),
      userId: credentialOptionsResponse.userId
    };
  }

  async createCredential(userId, options) {
    await this.waitForTokenRefresh();
    const bearerToken = sessionStorageService.get(SessionConstants.TokenKey);

    try {
      const attestationResponse = await create(options);
      const response = await axios.post('https://localhost:7214/v1/Auth/register/end', {
        attestationResponse: attestationResponse,
        userId: userId
      }, {
        headers: {
          'Content-Type': 'application/json',
          'User-Agent': navigator.userAgent,
          'Authorization': `Bearer ${bearerToken}`,
        }
      });
      return response.data;
    } catch (error) {
      if (error.response && error.response.status === 400) {
        throw new Error('Bad Request: ' + error.response.data);
      } else {
        throw new Error('Failed to create credential: ' + error.message);
      }
    }
  }

  async createAssertionOptions() {
    const response = await axios.post('https://localhost:7214/v1/Auth/autenticate/begin', null, {
      headers: {
        'Content-Type': 'application/json',
      }
    });
    if (response.status === 400) {
      throw new Error(response.data);
    }
    const assertionOptionsResponse = response.data;
    const abortController = new AbortController();

     console.log("RESPONSE Autenticate/begin", response);

    return {
      options: parseRequestOptionsFromJSON({
        publicKey: assertionOptionsResponse.assertionOptions,
        signal: abortController.signal
      }),
      userId: assertionOptionsResponse.userId
    };
  }

  async verifyAssertion(options) {
    const isConditionalMediationAvailable = (PublicKeyCredential && await PublicKeyCredential.isConditionalMediationAvailable());
    if (!isConditionalMediationAvailable) {
      throw new Error('Mediation is not supported :(');
    }
    const assertionResponse = await get(options);

    console.log("ASSERTION RESPONSE",assertionResponse );

    const response = await axios.post('https://localhost:7214/v1/Auth/autenticate/end', {
      assertionRawResponse: assertionResponse.toJSON()
    }, {
      headers: {
        'Content-Type': 'application/json',
        'User-Agent': navigator.userAgent,
      }
    });
    return response.data;
  }

  async revokeCredential(credentialId) {
    const token = sessionStorageService.get(SessionConstants.TokenKey);
    if (token === null) {
      throw new Error('Token expired!');
    }
    const response = await axios.delete('https://localhost:7214/api/fido2/credential', {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      },
      data: credentialId
    });
    if (response.status !== 204) {
      throw new Error(response.data);
    }
  }

  async waitForTokenRefresh() {
    return new Promise((resolve) => {
      const checkToken = () => {
        const token = sessionStorageService.get(SessionConstants.TokenKey);
        if (token !== null) {
          resolve();
        } else {
          setTimeout(checkToken, 100); // Verifica a cada 100ms se o token foi atualizado
        }
      };
      checkToken();
    });
  }
}

const passKeyService = new PassKeyService();
export default passKeyService;
