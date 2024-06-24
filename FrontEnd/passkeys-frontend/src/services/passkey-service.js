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
  async createCredentialOptions(userName) {
    const response = await axios.post('https://localhost:7214/api/fido2/credential-options', userName, {
      headers: {
        'Content-Type': 'application/json'
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
    const token = sessionStorageService.get(SessionConstants.TokenKey);
    if (token === null) {
      throw new Error('Token expired!');
    }
    const response = await axios.put('https://localhost:7214/api/fido2/credential-options', null, {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
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
    try {
      const attestationResponse = await create(options);
      const response = await axios.post('https://localhost:7214/api/fido2/credential', {
            attestationResponse: attestationResponse, 
            userId: userId
        }, {
            headers: {
                'Content-Type': 'application/json',
                'User-Agent': navigator.userAgent
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

  async createAssertionOptions(userName) {
    const response = await axios.post('https://localhost:7214/api/fido2/assertion-options', userName, {
      headers: {
        'Content-Type': 'application/json'
      }
    });
    if (response.status === 400) {
      throw new Error(response.data);
    }
    const assertionOptionsResponse = response.data;
    const abortController = new AbortController();
    return {
      options: parseRequestOptionsFromJSON({
        publicKey: assertionOptionsResponse.assertionOptions,
        signal: abortController.signal
      }),
      userId: assertionOptionsResponse.userId
    };
  }

  async verifyAssertion(userId, options) {
    const isConditionalMediationAvailable = (PublicKeyCredential && await PublicKeyCredential.isConditionalMediationAvailable());
    if (!isConditionalMediationAvailable) {
      throw new Error('Mediation is not supported :(');
    }
    const assertionResponse = await get(options);
    const response = await axios.post('https://localhost:7214/api/fido2/assertion', {
      assertionRawResponse: assertionResponse.toJSON(),
      userId: userId
    }, {
      headers: {
        'Content-Type': 'application/json',
        'User-Agent': navigator.userAgent
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
}

const passKeyService = new PassKeyService();
export default passKeyService;
