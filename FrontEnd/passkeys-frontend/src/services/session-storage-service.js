class SessionStorageService {
  get(key) {
      const item = sessionStorage.getItem(key);
      if (item == null) {
          return null;
      }
      const storedItem = JSON.parse(item);
      if (storedItem.expiry === null) {
          return storedItem.item;
      }
      if (storedItem.expiry > Date.now()) {
          return storedItem.item;
      }
      sessionStorage.removeItem(key);
      return null;
  }

  set(key, item, expiryInMilliSeconds = null) {
      let expiry = null;
      if (expiryInMilliSeconds !== null) {
          expiry = Date.now() + expiryInMilliSeconds;
      }
      sessionStorage.setItem(key, JSON.stringify({
          item,
          expiry
      }));
  }

  clear(key) {
      sessionStorage.removeItem(key);
  }
}

const sessionStorageService = new SessionStorageService();
export default sessionStorageService;
