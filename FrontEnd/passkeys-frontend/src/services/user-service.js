import sessionStorageService from '../services/session-storage-service';
import { SessionConstants } from '../constants';
import { format } from 'date-fns';

class Credential {
    constructor() {
        this.id = '';
        this.createdAtUtc = '';
        this.updatedAtUtc = '';
        this.lastUsedPlatformInfo = '';
    }
}

class User {
    constructor() {
        this.userName = '';
        this.credentials = [];
    }
}

class UserService {
    async getUser() {
        const token = sessionStorageService.get(SessionConstants.TokenKey);
        if (token === null) {
            throw new Error('Token expired!');
        }

        const response = await fetch('https://localhost:7214/api/users/me', {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`,
            },
        });

        if (response.status !== 200) {
            throw new Error(await response.text());
        }

        const user = await response.json();
        const dateFormat = 'dd-MMM-yyyy HH:mm aa';
        user.credentials = user.credentials.map((credential) => {
            try {
                credential.createdAtUtc = format(new Date(credential.createdAtUtc), dateFormat);
                credential.updatedAtUtc = format(new Date(credential.updatedAtUtc), dateFormat);
            } catch (e) {
                console.log(e);
            }
            return credential;
        });

        return user;
    }
}

const userService = new UserService();
export default userService;
